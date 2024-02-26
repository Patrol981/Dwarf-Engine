using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;
using Dwarf.Vulkan;

using ImGuiNET;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.UI;
public partial class ImGuiController : IDisposable {
  private readonly VulkanDevice _device;
  private readonly Renderer _renderer;

  private DwarfBuffer _vertexBuffer;
  private DwarfBuffer _indexBuffer;
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
  protected VulkanDescriptorWriter _descriptorWriter;

  private VulkanTexture _fontTexture;

  // private int _vertexBufferSize = 0;
  // private int _indexBufferSize = 0;

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
      .SetMaxSets(100)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 100)
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
    CreateStyles();

    ImGui.NewFrame();
    _frameBegun = true;

    WindowState.s_Window.OnResizedEventDispatcher += WindowResized;
  }

  private void CreateStyles() {
    var colors = ImGui.GetStyle().Colors;
    colors[(int)ImGuiCol.Text] = new(1.00f, 1.00f, 1.00f, 1.00f);
    colors[(int)ImGuiCol.TextDisabled] = new(0.50f, 0.50f, 0.50f, 1.00f);
    colors[(int)ImGuiCol.WindowBg] = new(0.10f, 0.10f, 0.10f, 1.00f);
    colors[(int)ImGuiCol.ChildBg] = new(0.00f, 0.00f, 0.00f, 0.00f);
    colors[(int)ImGuiCol.PopupBg] = new(0.19f, 0.19f, 0.19f, 0.92f);
    colors[(int)ImGuiCol.Border] = new(0.19f, 0.19f, 0.19f, 0.29f);
    colors[(int)ImGuiCol.BorderShadow] = new(0.00f, 0.00f, 0.00f, 0.24f);
    colors[(int)ImGuiCol.FrameBg] = new(0.05f, 0.05f, 0.05f, 0.54f);
    colors[(int)ImGuiCol.FrameBgHovered] = new(0.19f, 0.19f, 0.19f, 0.54f);
    colors[(int)ImGuiCol.FrameBgActive] = new(0.20f, 0.22f, 0.23f, 1.00f);
    colors[(int)ImGuiCol.TitleBg] = new(0.00f, 0.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.TitleBgActive] = new(0.06f, 0.06f, 0.06f, 1.00f);
    colors[(int)ImGuiCol.TitleBgCollapsed] = new(0.00f, 0.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.MenuBarBg] = new(0.14f, 0.14f, 0.14f, 1.00f);
    colors[(int)ImGuiCol.ScrollbarBg] = new(0.05f, 0.05f, 0.05f, 0.54f);
    colors[(int)ImGuiCol.ScrollbarGrab] = new(0.34f, 0.34f, 0.34f, 0.54f);
    colors[(int)ImGuiCol.ScrollbarGrabHovered] = new(0.40f, 0.40f, 0.40f, 0.54f);
    colors[(int)ImGuiCol.ScrollbarGrabActive] = new(0.56f, 0.56f, 0.56f, 0.54f);
    colors[(int)ImGuiCol.CheckMark] = new(0.33f, 0.67f, 0.86f, 1.00f);
    colors[(int)ImGuiCol.SliderGrab] = new(0.34f, 0.34f, 0.34f, 0.54f);
    colors[(int)ImGuiCol.SliderGrabActive] = new(0.56f, 0.56f, 0.56f, 0.54f);
    colors[(int)ImGuiCol.Button] = new(0.859f, 0.369f, 0.231f, 1f);
    colors[(int)ImGuiCol.ButtonHovered] = new(0.19f, 0.19f, 0.19f, 1f);
    colors[(int)ImGuiCol.ButtonActive] = new(0.20f, 0.22f, 0.23f, 1.00f);
    colors[(int)ImGuiCol.Header] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.HeaderHovered] = new(0.00f, 0.00f, 0.00f, 0.36f);
    colors[(int)ImGuiCol.HeaderActive] = new(0.20f, 0.22f, 0.23f, 0.33f);
    colors[(int)ImGuiCol.Separator] = new(0.28f, 0.28f, 0.28f, 0.29f);
    colors[(int)ImGuiCol.SeparatorHovered] = new(0.44f, 0.44f, 0.44f, 0.29f);
    colors[(int)ImGuiCol.SeparatorActive] = new(0.40f, 0.44f, 0.47f, 1.00f);
    colors[(int)ImGuiCol.ResizeGrip] = new(0.28f, 0.28f, 0.28f, 0.29f);
    colors[(int)ImGuiCol.ResizeGripHovered] = new(0.44f, 0.44f, 0.44f, 0.29f);
    colors[(int)ImGuiCol.ResizeGripActive] = new(0.40f, 0.44f, 0.47f, 1.00f);
    colors[(int)ImGuiCol.Tab] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TabHovered] = new(0.14f, 0.14f, 0.14f, 1.00f);
    colors[(int)ImGuiCol.TabActive] = new(0.20f, 0.20f, 0.20f, 0.36f);
    colors[(int)ImGuiCol.TabUnfocused] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TabUnfocusedActive] = new(0.14f, 0.14f, 0.14f, 1.00f);
    colors[(int)ImGuiCol.DockingPreview] = new(0.33f, 0.67f, 0.86f, 1.00f);
    colors[(int)ImGuiCol.DockingEmptyBg] = new(1.00f, 0.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotLines] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotLinesHovered] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotHistogram] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotHistogramHovered] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.TableHeaderBg] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TableBorderStrong] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TableBorderLight] = new(0.28f, 0.28f, 0.28f, 0.29f);
    colors[(int)ImGuiCol.TableRowBg] = new(0.00f, 0.00f, 0.00f, 0.00f);
    colors[(int)ImGuiCol.TableRowBgAlt] = new(1.00f, 1.00f, 1.00f, 0.06f);
    colors[(int)ImGuiCol.TextSelectedBg] = new(0.20f, 0.22f, 0.23f, 1.00f);
    colors[(int)ImGuiCol.DragDropTarget] = new(0.33f, 0.67f, 0.86f, 1.00f);
    colors[(int)ImGuiCol.NavHighlight] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.NavWindowingHighlight] = new(1.00f, 1.00f, 0.00f, 0.70f);
    colors[(int)ImGuiCol.NavWindowingDimBg] = new(1.00f, 1.00f, 0.00f, 0.20f);
    colors[(int)ImGuiCol.ModalWindowDimBg] = new(1.00f, 1.00f, 0.00f, 0.35f);

    var style = ImGui.GetStyle();
    style.WindowPadding = new(8.00f, 8.00f);
    style.FramePadding = new(5.00f, 2.00f);
    style.CellPadding = new(6.00f, 6.00f);
    style.ItemSpacing = new(6.00f, 6.00f);
    style.ItemInnerSpacing = new(6.00f, 6.00f);
    style.TouchExtraPadding = new(0.00f, 0.00f);
    style.IndentSpacing = 25;
    style.ScrollbarSize = 15;
    style.GrabMinSize = 10;
    style.WindowBorderSize = 1;
    style.ChildBorderSize = 1;
    style.PopupBorderSize = 1;
    style.FrameBorderSize = 1;
    style.TabBorderSize = 1;
    style.WindowRounding = 0;
    style.ChildRounding = 4;
    style.FrameRounding = 3;
    style.PopupRounding = 4;
    style.ScrollbarRounding = 9;
    style.GrabRounding = 3;
    style.LogSliderDeadzone = 4;
    style.TabRounding = 4;
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

    if ((_vertexBuffer.GetBuffer() == VkBuffer.Null) || (_vertexCount < drawData.TotalVtxCount)) {
      _vertexCount = drawData.TotalVtxCount;

      _vertexBuffer?.Dispose();
      _vertexBuffer = new(
        _device,
        (ulong)sizeof(ImDrawVert),
        (ulong)drawData.TotalVtxCount,
        BufferUsage.VertexBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );

      // _vertexBuffer.Map((ulong)vertexBufferSize);
      //_vertexBuffer.Map();
    }

    if ((_indexBuffer.GetBuffer() == VkBuffer.Null) || (_indexCount < drawData.TotalIdxCount)) {
      _indexCount = drawData.TotalIdxCount;

      _indexBuffer?.Dispose();
      _indexBuffer = new(
        _device,
        (ulong)sizeof(ushort),
        (ulong)drawData.TotalIdxCount,
        BufferUsage.IndexBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );

      // _indexBuffer.Map((ulong)indexBufferSize);
      // _indexBuffer.Map();
    }

    // var vtxDst = _vertexBuffer.GetMappedMemory();
    // var idxDst = _indexBuffer.GetMappedMemory();

    ImDrawVert* vtxDst = null;
    ushort* idxDst = null;

    vkMapMemory(_device.LogicalDevice, _vertexBuffer.GetVkDeviceMemory(), 0, _vertexBuffer.GetBufferSize(), 0, (void**)&vtxDst);
    vkMapMemory(_device.LogicalDevice, _indexBuffer.GetVkDeviceMemory(), 0, _indexBuffer.GetBufferSize(), 0, (void**)&idxDst);

    var vtxOffset = 0;
    var idxOffset = 0;


    nint totalVtxData = 0;
    nint totalIdxData = 0;

    var totalVtxSize = drawData.TotalVtxCount * sizeof(ImDrawVert);
    var totalIdxSize = drawData.TotalIdxCount * sizeof(ushort);

    for (int n = 0; n < drawData.CmdListsCount; n++) {
      var cmdList = drawData.CmdLists[n];

      // MemoryUtils.MemCopy(vtxDst, cmdList.VtxBuffer.Data, vertexBufferSize);
      // MemoryUtils.MemCopy(idxDst, cmdList.IdxBuffer.Data, indexBufferSize);
      var vtxSize = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
      var idxSize = cmdList.IdxBuffer.Size * sizeof(ushort);
      // _vertexBuffer.WriteToBuffer(cmdList.VtxBuffer.Data, (ulong)vtxSize, (ulong)vtxOffset);
      //_indexBuffer.WriteToBuffer(cmdList.IdxBuffer.Data, (ulong)idxSize, (ulong)idxOffset);

      Unsafe.CopyBlock(vtxDst, cmdList.VtxBuffer.Data.ToPointer(), (uint)cmdList.VtxBuffer.Size * (uint)sizeof(ImDrawVert));
      Unsafe.CopyBlock(idxDst, cmdList.IdxBuffer.Data.ToPointer(), (uint)cmdList.IdxBuffer.Size * sizeof(ushort));

      // _vertexBuffer.WrtieToIndex(cmdList.VtxBuffer.Data, n);
      // _indexBuffer.WrtieToIndex(cmdList.IdxBuffer.Data, n);

      // totalIdxData += cmdList.IdxBuffer.Data;
      // totalVtxData += cmdList.VtxBuffer.Data;

      vtxDst += cmdList.VtxBuffer.Size;
      idxDst += cmdList.IdxBuffer.Size;

      // vtxOffset += cmdList.VtxBuffer.Size;
      // idxOffset += cmdList.IdxBuffer.Size;
    }

    // _vertexBuffer.Unmap();
    // _indexBuffer.Unmap();

    vkUnmapMemory(_device.LogicalDevice, _vertexBuffer.GetVkDeviceMemory());
    vkUnmapMemory(_device.LogicalDevice, _indexBuffer.GetVkDeviceMemory());

    // _vertexBuffer.Flush((ulong)vertexBufferSize);
    // _indexBuffer.Flush((ulong)indexBufferSize);
  }

  public unsafe void RenderImDrawData(ImDrawDataPtr drawData, FrameInfo frameInfo) {
    // update buffers

    UpdateBuffers(drawData);

    BindTexture(frameInfo);
    BindShaderData(frameInfo);

    int vertexOffset = 0;
    uint indexOffset = 0;

    if (drawData.CmdListsCount > 0) {
      ulong[] offsets = [0];
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
          SetScissorRect(frameInfo, pcmd, drawData);
          // vkCmdDrawIndexed(frameInfo.CommandBuffer, pcmd.ElemCount, 1, indexOffset, vertexOffset, 0);

          vkCmdDrawIndexed(
            frameInfo.CommandBuffer,
            pcmd.ElemCount,
            1,
            pcmd.IdxOffset + indexOffset,
            (int)pcmd.VtxOffset + vertexOffset,
            0
          );

          // vkCmdDraw(frameInfo.CommandBuffer, pcmd.VtxOffset + (uint)vertexOffset, 1, 0, 0);
          // vkCmdDraw(frameInfo.CommandBuffer, (uint)_vertexCount, 1, 0, 0);
          // indexOffset += pcmd.ElemCount;
        }
        indexOffset += (uint)cmdList.IdxBuffer.Size;
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
