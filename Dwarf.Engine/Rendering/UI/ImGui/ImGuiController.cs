using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using ImGuiNET;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.UI;
public partial class ImGuiController : IDisposable {
  private readonly VulkanDevice _device;
  private readonly Renderer _renderer;

  private Vulkan.Buffer _vertexBuffer;
  private Vulkan.Buffer _indexBuffer;
  private int _vertexCount;
  private int _indexCount;

  // custom
  private VkSampler _sampler;
  private VkDeviceMemory _fontMemory = VkDeviceMemory.Null;
  private VkImage _fontImage = VkImage.Null;
  private VkImageView _fontView = VkImageView.Null;
  private VkPipelineCache _pipelineCache;
  // private VkPipelineLayout _pipelineLayout;
  // private VkPipeline _pipeline;
  // private VkDescriptorPool _descriptorPool;
  // private VkDescriptorSetLayout _descriptorSetLayout;
  // private VkDescriptorSet _descriptorSet;
  // private VkPhysicalDeviceDriverProperties _driverProperties;
  // private ImGuiStylePtr _vulkanStyle;
  // private VkShaderModule _vertexModule = VkShaderModule.Null;
  // private VkShaderModule _fragmentModule = VkShaderModule.Null;

  // system based
  protected PipelineConfigInfo _pipelineConfigInfo;
  protected VkPipelineLayout _systemPipelineLayout;
  protected Pipeline _systemPipeline = null!;
  protected DescriptorPool _systemDescriptorPool = null!;
  protected DescriptorSetLayout _systemSetLayout = null!;
  protected VkDescriptorSet _systemDescriptorSet;
  protected DescriptorWriter _descriptorWriter;

  private Texture _fontTexture;

  private int _vertexBufferSize = 0;
  private int _indexBufferSize = 0;

  private bool _frameBegun = false;

  private int _width;
  private int _height;
  private System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;
  private VkFrontFace _frontFace = VkFrontFace.Clockwise;

  [StructLayout(LayoutKind.Explicit)]
  struct ImGuiPushConstant {
    [FieldOffset(0)] public Matrix4x4 Projection;
  }

  public unsafe ImGuiController(VulkanDevice device, Renderer renderer) {
    _device = device;
    _renderer = renderer;

    ImGui.CreateContext();
  }

  public unsafe void InitResources() {
    var descriptorCount = (uint)_renderer.MAX_FRAMES_IN_FLIGHT * 2;

    _systemDescriptorPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(descriptorCount * 2)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, descriptorCount)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.None)
      .Build();

    _systemSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.All)
      .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      _systemSetLayout.GetDescriptorSetLayout()
    ];

    InitTexture(_device.GraphicsQueue);

    VkPipelineCacheCreateInfo pipelineCacheCreateInfo = new();
    vkCreatePipelineCache(_device.LogicalDevice, &pipelineCacheCreateInfo, null, out _pipelineCache).CheckResult();

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(_renderer.GetSwapchainRenderPass(), "imgui_vertex", "imgui_fragment", new PipelineImGuiProvider());
  }

  public void Init(int width, int height) {
    _width = width;
    _height = height;

    CreateBuffers();

    IntPtr context = ImGui.CreateContext();
    ImGui.SetCurrentContext(context);
    var io = ImGui.GetIO();
    io.Fonts.AddFontDefault();

    io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    io.DisplaySize = new(width, height);
    io.DisplayFramebufferScale = new(1.0f, 1.0f);

    // InitResources(_renderer.GetSwapchainRenderPass(), _device.GraphicsQueue, "imgui_vertex", "imgui_fragment");
    InitResources();

    SetPerFrameImGuiData(1f / 60f);

    ImGui.NewFrame();
    _frameBegun = true;

    WindowState.s_Window.OnResizedEventDispatcher += WindowResized;
  }

  private void WindowResized(object? sender, EventArgs e) {
    var windowExtent = WindowState.s_Window.Extent;
    _width = (int)windowExtent.Width;
    _height = (int)windowExtent.Height;
    Logger.Info($"[ImGUI] Window Resized ({_width}{_height})");
  }

  private void SetPerFrameImGuiData(float deltaSeconds) {
    ImGuiIOPtr io = ImGui.GetIO();
    io.DisplaySize = new System.Numerics.Vector2(
      _width / _scaleFactor.X,
      _height / _scaleFactor.Y);
    io.DisplayFramebufferScale = _scaleFactor;
    io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
  }

  public void Render(FrameInfo frameInfo) {
    if (_frameBegun) {
      _frameBegun = false;
      ImGui.Render();
      RenderImDrawData(ImGui.GetDrawData(), frameInfo);
    }
  }

  public void Update(float deltaSeconds) {
    if (_frameBegun) {
      ImGui.Render();
    }

    SetPerFrameImGuiData(deltaSeconds);
    UpdateImGuiInput();

    _frameBegun = true;
    ImGui.NewFrame();
  }

  public void UpdateImGuiInput() {
    ImGuiIOPtr io = ImGui.GetIO();
    io.MouseDown[0] = MouseState.GetInstance().QuickStateMouseButtons.Left;
    io.MouseDown[1] = MouseState.GetInstance().QuickStateMouseButtons.Right;
    io.MouseDown[2] = MouseState.GetInstance().QuickStateMouseButtons.Middle;
    var screenPoint = new Vector2((int)MouseState.GetInstance().MousePosition.X, (int)MouseState.GetInstance().MousePosition.Y);
    io.MousePos = new System.Numerics.Vector2(screenPoint.X, screenPoint.Y);
  }

  public unsafe void UpdateBuffers(ImDrawDataPtr drawData) {
    var vertexBufferSize = drawData.TotalVtxCount * sizeof(ImDrawVert);
    var indexBufferSize = drawData.TotalIdxCount * sizeof(ushort);

    if ((vertexBufferSize == 0) || (indexBufferSize == 0)) {
      return;
    }

    if (_vertexBuffer.GetBuffer() == VkBuffer.Null || _vertexCount != drawData.TotalVtxCount) {
      _vertexBuffer?.Dispose();
      _vertexBuffer = new(
        _device,
        (ulong)vertexBufferSize,
        BufferUsage.VertexBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
      _vertexBuffer.Map((ulong)vertexBufferSize);
    }

    if (_indexBuffer.GetBuffer() == VkBuffer.Null || _indexCount < drawData.TotalIdxCount) {
      _indexBuffer?.Dispose();
      _indexBuffer = new(
        _device,
        (ulong)indexBufferSize,
        BufferUsage.IndexBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
      _indexBuffer.Map((ulong)indexBufferSize);
    }

    var vtxDst = _vertexBuffer.GetMappedMemory();
    var idxDst = _indexBuffer.GetMappedMemory();

    for (int n = 0; n < drawData.CmdListsCount; n++) {
      var cmdList = drawData.CmdLists[n];

      VkUtils.MemCopy(vtxDst, cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * sizeof(ImDrawVert));
      VkUtils.MemCopy(idxDst, cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * sizeof(ushort));

      vtxDst += cmdList.VtxBuffer.Size;
      idxDst += cmdList.IdxBuffer.Size;
    }

    // _vertexBuffer.Flush();
    // _indexBuffer.Flush();
  }

  public unsafe void RenderImDrawData(ImDrawDataPtr drawData, FrameInfo frameInfo) {
    // update buffers

    UpdateBuffers(drawData);

    BindTexture(frameInfo);
    BindShaderData(frameInfo);

    int vertexOffset = 0;
    uint indexOffset = 0;

    if (drawData.CmdListsCount > 0) {
      ulong[] offsets = { 0 };
      VkBuffer[] vertexBuffers = [_vertexBuffer.GetBuffer()];

      fixed (VkBuffer* vertexPtr = vertexBuffers)
      fixed (ulong* offsetsPtr = offsets) {
        vkCmdBindVertexBuffers(frameInfo.CommandBuffer, 0, 1, vertexPtr, offsetsPtr);
      }
      vkCmdBindIndexBuffer(frameInfo.CommandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint16);

      for (int i = 0; i < drawData.CmdListsCount; i++) {
        var cmdList = drawData.CmdLists[i];
        for (int j = 0; j < cmdList.CmdBuffer.Size; j++) {
          var pcmd = cmdList.CmdBuffer[j];
          SetScissorRect(frameInfo, pcmd);
          vkCmdDrawIndexed(frameInfo.CommandBuffer, pcmd.ElemCount, 1, indexOffset, vertexOffset, 0);
          indexOffset += pcmd.ElemCount;
        }
        vertexOffset += cmdList.VtxBuffer.Size;
      }
    }

  }

  private unsafe void CreateShaderModule(byte[] data, out VkShaderModule module) {
    vkCreateShaderModule(_device.LogicalDevice, data, null, out module).CheckResult();
  }

  public unsafe void Dispose() {
    ImGui.DestroyContext();
    _vertexBuffer?.Dispose();
    _indexBuffer?.Dispose();

    vkDestroyImage(_device.LogicalDevice, _fontImage, null);
    vkDestroyImageView(_device.LogicalDevice, _fontView, null);
    vkFreeMemory(_device.LogicalDevice, _fontMemory, null);
    vkDestroySampler(_device.LogicalDevice, _sampler, null);

    _fontTexture?.Dispose();
    _systemPipeline?.Dispose();
    _systemDescriptorPool?.Dispose();
    _systemSetLayout?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _systemPipelineLayout);
    vkDestroyPipelineCache(_device.LogicalDevice, _pipelineCache, null);

    /*
    vkDestroyShaderModule(_device.LogicalDevice, _vertexModule, null);
    vkDestroyShaderModule(_device.LogicalDevice, _fragmentModule, null);
    
    
    vkDestroyPipeline(_device.LogicalDevice, _pipeline, null);
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout, null);
    vkDestroyDescriptorPool(_device.LogicalDevice, _descriptorPool, null);
    vkDestroyDescriptorSetLayout(_device.LogicalDevice, _descriptorSetLayout, null);
    */
  }
}
