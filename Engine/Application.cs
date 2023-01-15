using System.Net.Mime;
using System.Numerics;
using System.Runtime.InteropServices;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public unsafe class Application {
  public delegate void EventCallback();

  public void SetUpdateCallback(EventCallback eventCallback) {
    _onUpdate = eventCallback;
  }

  public void SetRenderCallback(EventCallback eventCallback) {
    _onRender = eventCallback;
  }

  public void SetGUICallback(EventCallback eventCallback) {
    _onGUI = eventCallback;
  }

  public void SetOnLoadCallback(EventCallback eventCallback) {
    _onLoad = eventCallback;
  }

  private EventCallback? _onUpdate;
  private EventCallback? _onRender;
  private EventCallback? _onGUI;
  private EventCallback? _onLoad;

  private Window _window = null!;
  private Pipeline _pipeline = null!;
  private Device _device = null!;
  private Swapchain _swapchain = null!;
  private VkPipelineLayout _pipelineLayout;
  private VkCommandBuffer[] _commandBuffers;


  private Model _model;

  public Application() {
    _window = new Window(1200, 900);
    _device = new Device(_window);
    _swapchain = new Swapchain(_device, _window.Extent);
    LoadModels();
    CreatePipelineLayout();
    CreatePipeline();
    CreateCommandBuffers();
    Run();
  }

  public void Run() {
    while (!_window.ShouldClose) {
      glfwPollEvents();
      DrawFrame();
    }

    var result = vkDeviceWaitIdle(_device.LogicalDevice);
    if (result != VkResult.Success) {
      Logger.Error(result.ToString());
    }
    Cleanup();
  }

  private void Init() {
    LoadModels();
  }

  private void LoadModels() {
    Vertex[] vertices = new Vertex[3];
    vertices[0] = new();
    vertices[1] = new();
    vertices[2] = new();

    vertices[0].Position = new System.Numerics.Vector2(0.0f, -0.5f);
    vertices[1].Position = new System.Numerics.Vector2(0.5f, 0.5f);
    vertices[2].Position = new System.Numerics.Vector2(-0.5f, 0.5f);

    vertices[0].Color = new Vector3(1.0f, 0.0f, 0.0f);
    vertices[1].Color = new Vector3(0.0f, 1.0f, 0.0f);
    vertices[2].Color = new Vector3(0.0f, 0.0f, 1.0f);

    //List<Vertex> sVertices = new();
    //int depth = 10;
    //System.Numerics.Vector2 left = new(-0.5f, 0.5f);
    //System.Numerics.Vector2 right = new(0.5f, 0.5f);
    //System.Numerics.Vector2 top = new(0.5f, -0.5f);

    // ierpinski(depth, left, right, top, ref sVertices);

    _model = new Model(_device, vertices);
  }

  private void Sierpinski(int depth, Vector2 left, Vector2 right, Vector2 top, ref List<Vertex> vertices) {
    if (depth <= 0) {
      var v = new Vertex[3];
      v[0].Position = top;
      v[1].Position = right;
      v[2].Position = left;
      vertices.AddRange(v);
    } else {
      var leftTop = 0.5f * (left + top);
      var rightTop = 0.5f * (right + top);
      var leftRight = 0.5f * (left + right);

      Sierpinski(depth - 1, left, leftRight, leftTop, ref vertices);
      Sierpinski(depth - 1, leftRight, right, rightTop, ref vertices);
      Sierpinski(depth - 1, leftTop, rightTop, top, ref vertices);
    }
  }

  private void CreatePipelineLayout() {
    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
    pipelineInfo.setLayoutCount = 0;
    pipelineInfo.pSetLayouts = null;
    pipelineInfo.pushConstantRangeCount = 0;
    pipelineInfo.pPushConstantRanges = null;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private void CreatePipeline() {
    PipelineConfigInfo configInfo = new();
    var pipelineConfig = Pipeline.DefaultConfigInfo(configInfo, (int)_swapchain.Extent2D.width, (int)_swapchain.Extent2D.height);
    pipelineConfig.RenderPass = _swapchain.RenderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "vertex", "fragment", pipelineConfig);
  }

  private void CreateCommandBuffers() {
    _commandBuffers = new VkCommandBuffer[_swapchain.ImageCount];

    VkCommandBufferAllocateInfo allocInfo = new();
    allocInfo.sType = VkStructureType.CommandBufferAllocateInfo;
    allocInfo.level = VkCommandBufferLevel.Primary;
    allocInfo.commandPool = _device.CommandPool;
    allocInfo.commandBufferCount = (uint)_commandBuffers.Length;

    GCHandle handle = GCHandle.Alloc(_commandBuffers, GCHandleType.Pinned);
    IntPtr ptr = handle.AddrOfPinnedObject();
    VkCommandBuffer* bufferPtr = (VkCommandBuffer*)ptr;

    vkAllocateCommandBuffers(_device.LogicalDevice, &allocInfo, bufferPtr).CheckResult();

    for (int i = 0; i < _commandBuffers.Length; i++) {
      VkCommandBufferBeginInfo beginInfo = new();
      beginInfo.sType = VkStructureType.CommandBufferBeginInfo;

      vkBeginCommandBuffer(_commandBuffers[i], &beginInfo).CheckResult();

      VkRenderPassBeginInfo renderPassInfo = new();
      renderPassInfo.sType = VkStructureType.RenderPassBeginInfo;
      renderPassInfo.renderPass = _swapchain.RenderPass;
      renderPassInfo.framebuffer = _swapchain.GetFramebuffer(i);

      renderPassInfo.renderArea.offset = new VkOffset2D(0, 0);
      renderPassInfo.renderArea.extent = _swapchain.Extent2D;

      VkClearValue* values = stackalloc VkClearValue[2];
      values[0].color = new VkClearColorValue(0.1f, 0.1f, 0.1f, 1.0f);
      values[1].depthStencil = new(1.0f, 0);
      renderPassInfo.clearValueCount = 2;
      renderPassInfo.pClearValues = values;

      vkCmdBeginRenderPass(_commandBuffers[i], &renderPassInfo, VkSubpassContents.Inline);

      _pipeline.Bind(_commandBuffers[i]);
      // vkCmdDraw(_commandBuffers[i], 3)
      _model.Bind(_commandBuffers[i]);
      _model.Draw(_commandBuffers[i]);

      vkCmdEndRenderPass(_commandBuffers[i]);
      vkEndCommandBuffer(_commandBuffers[i]).CheckResult();
    }
  }

  private void DrawFrame() {
    uint imageIndex;
    var result = _swapchain.AcquireNextImage(out imageIndex);

    if (result != VkResult.Success && result != VkResult.SuboptimalKHR) {
      Logger.Error("Failed to acquire next swapchain image");
    }

    GCHandle handle = GCHandle.Alloc(_commandBuffers, GCHandleType.Pinned);
    IntPtr ptr = handle.AddrOfPinnedObject();
    IntPtr elementPtr = new IntPtr(ptr.ToInt64() + imageIndex * Marshal.SizeOf(typeof(VkCommandBuffer)));
    VkCommandBuffer* bufferPtr = (VkCommandBuffer*)elementPtr;

    result = _swapchain.SubmitCommandBuffers(bufferPtr, imageIndex);
    if (result != VkResult.Success) {
      Logger.Error("Error while submitting Command buffer");
    }
  }

  private void Cleanup() {
    _model.Dispose();
    for (int i = 0; i < _commandBuffers.Length; i++) {
      vkFreeCommandBuffers(_device.LogicalDevice, _device.CommandPool, _commandBuffers[i]);
    }
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
    _swapchain?.Dispose();
    _window?.Dispose();
    _device?.Dispose();
  }
}