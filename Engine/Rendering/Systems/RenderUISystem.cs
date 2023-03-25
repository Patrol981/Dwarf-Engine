using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using static Dwarf.Extensions.GLFW.GLFWKeyMap;

using ImGuiNET;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;
using DwarfEngine.Engine;
using Assimp;

namespace Dwarf.Engine.Rendering;

public class RenderUISystem : SystemBase, IRenderSystem {
  private readonly Device _device;

  private PipelineConfigInfo _configInfo = null!;
  private Pipeline _pipeline = null!;
  private VkPipelineLayout _pipelineLayout;

  private bool _frameBegun = false;
  private int _windowWidth;
  private int _windowHeight;
  private System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;

  private IntPtr _fontAtlasID = (IntPtr)1;

  // Buffers
  private Dwarf.Vulkan.Buffer _vertexBuffer = null!;
  private Dwarf.Vulkan.Buffer _indexBuffer = null!;

  // Descriptors
  private DescriptorPool _uiPool;
  private DescriptorSetLayout _uiLayout;

  // Texturing
  private VkImageView _imageView;

  public RenderUISystem() { }

  public RenderUISystem(Device device, VkRenderPass renderPass) {
    _device = device;

    _uiLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .AddBinding(1, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
      .AddBinding(2, VkDescriptorType.Sampler, VkShaderStageFlags.Fragment)
      .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = new VkDescriptorSetLayout[] {
      _uiLayout.GetDescriptorSetLayout()
    };

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(renderPass);
  }

  public override IRenderSystem Create(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSet,
    PipelineConfigInfo configInfo = null!
  ) {
    return new RenderUISystem(device, renderer.GetSwapchainRenderPass());
  }

  public void SetupUIData(int uiElements, int width, int height) {
    _windowHeight = height;
    _windowWidth = width;

    _uiPool = new DescriptorPool.Builder(_device)
    .SetMaxSets((uint)uiElements)
    .AddPoolSize(VkDescriptorType.Sampler, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.SampledImage, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.StorageImage, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.UniformTexelBuffer, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.StorageTexelBuffer, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.StorageBuffer, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.UniformBufferDynamic, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.StorageBufferDynamic, (uint)uiElements)
    .AddPoolSize(VkDescriptorType.InputAttachment, (uint)uiElements)
    .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
    .Build();

    ImGui.CreateContext();
    // ImGui.SetCurrentContext(ctx);
    var io = ImGui.GetIO();

    io.Fonts.AddFontDefault();

    io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
    io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;

    ImGui.StyleColorsDark();

    CreateDeviceResources();
    SetKeyMappings();

    SetPerFrameImGuiData(1f / 60f);

    // ImGui.NewFrame();
    _frameBegun = true;
  }

  public void DrawUI() {
    // ImGui.Render();
    // Console.WriteLine("UI LOOP");
  }

  public void WindowResized(int width, int height) {
    _windowWidth = width;
    _windowHeight = height;
  }

  private void SetPerFrameImGuiData(float deltaSeconds) {
    var io = ImGui.GetIO();
    io.DisplaySize = new System.Numerics.Vector2(
      _windowWidth / _scaleFactor.X,
      _windowHeight / _scaleFactor.Y);
    io.DisplayFramebufferScale = _scaleFactor;
    io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
  }

  private void SetKeyMappings() {
    ImGuiIOPtr io = ImGui.GetIO();
    io.KeyMap[(int)ImGuiKey.Tab] = (int)Keys.GLFW_KEY_TAB;
    io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keys.GLFW_KEY_LEFT;
    io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Keys.GLFW_KEY_RIGHT;
    io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Keys.GLFW_KEY_UP;
    io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Keys.GLFW_KEY_DOWN;
    io.KeyMap[(int)ImGuiKey.PageUp] = (int)Keys.GLFW_KEY_PAGE_UP;
    io.KeyMap[(int)ImGuiKey.PageDown] = (int)Keys.GLFW_KEY_PAGE_DOWN;
    io.KeyMap[(int)ImGuiKey.Home] = (int)Keys.GLFW_KEY_HOME;
    io.KeyMap[(int)ImGuiKey.End] = (int)Keys.GLFW_KEY_END;
    io.KeyMap[(int)ImGuiKey.Delete] = (int)Keys.GLFW_KEY_DELETE;
    io.KeyMap[(int)ImGuiKey.Backspace] = (int)Keys.GLFW_KEY_BACKSPACE;
    io.KeyMap[(int)ImGuiKey.Enter] = (int)Keys.GLFW_KEY_ENTER;
    io.KeyMap[(int)ImGuiKey.Escape] = (int)Keys.GLFW_KEY_ESCAPE;
    io.KeyMap[(int)ImGuiKey.A] = (int)Keys.GLFW_KEY_A;
    io.KeyMap[(int)ImGuiKey.C] = (int)Keys.GLFW_KEY_C;
    io.KeyMap[(int)ImGuiKey.V] = (int)Keys.GLFW_KEY_V;
    io.KeyMap[(int)ImGuiKey.X] = (int)Keys.GLFW_KEY_X;
    io.KeyMap[(int)ImGuiKey.Y] = (int)Keys.GLFW_KEY_Y;
    io.KeyMap[(int)ImGuiKey.Z] = (int)Keys.GLFW_KEY_Z;
  }

  private void CreateDeviceResources() {
    _vertexBuffer = new Vulkan.Buffer(
      _device,
      10000,
      1,
      VkBufferUsageFlags.VertexBuffer,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    _indexBuffer = new Vulkan.Buffer(
      _device,
      2000,
      1,
      VkBufferUsageFlags.IndexBuffer,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );


  }

  private void RecreateFontDeviceTexture() {
    ImGuiIOPtr io = ImGui.GetIO();
    IntPtr pixels;
    int width, height, bytesPerPixel;
    io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height, out bytesPerPixel);
    // Store our identifier
    io.Fonts.SetTexID(_fontAtlasID);

  }

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    if (_configInfo == null) {
      _configInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _configInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "gui_vertex", "gui_fragment", pipelineConfig, new PipelineUIProvider());
  }

  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
    pipelineInfo.setLayoutCount = (uint)layouts.Length;
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
    pipelineInfo.pushConstantRangeCount = 0;
    pipelineInfo.pPushConstantRanges = null;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    // _uiPool?.FreeDescriptors();
    _uiPool?.Dispose();
    _uiLayout?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
    _vertexBuffer?.Dispose();
    _indexBuffer?.Dispose();
  }
}