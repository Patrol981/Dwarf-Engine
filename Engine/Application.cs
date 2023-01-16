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
    RecreateSwapchain();
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

    _model = new Model(_device, vertices);
  }

  private void RecreateSwapchain() {
    var extent = _window.Extent;
    while (extent.width == 0 || extent.height == 0) {
      extent = _window.Extent;
      glfwWaitEvents();
    }

    vkDeviceWaitIdle(_device.LogicalDevice);

    if (_swapchain == null) {
      _swapchain?.Dispose();
      _swapchain = new(_device, extent);
    } else {
      var copy = _swapchain;
      _swapchain = new(_device, extent, ref copy);
      if (_commandBuffers == null || _swapchain.ImageCount != _commandBuffers.Length) {
        FreeCommandBuffers();
        CreateCommandBuffers();
      }
    }

    CreatePipeline();
  }

  private void RecordCommandBuffer(int imageIndex) {
    VkCommandBufferBeginInfo beginInfo = new();
    beginInfo.sType = VkStructureType.CommandBufferBeginInfo;

    vkBeginCommandBuffer(_commandBuffers[imageIndex], &beginInfo).CheckResult();

    VkRenderPassBeginInfo renderPassInfo = new();
    renderPassInfo.sType = VkStructureType.RenderPassBeginInfo;
    renderPassInfo.renderPass = _swapchain.RenderPass;
    renderPassInfo.framebuffer = _swapchain.GetFramebuffer(imageIndex);

    renderPassInfo.renderArea.offset = new VkOffset2D(0, 0);
    renderPassInfo.renderArea.extent = _swapchain.Extent2D;

    VkClearValue* values = stackalloc VkClearValue[2];
    values[0].color = new VkClearColorValue(0.1f, 0.1f, 0.1f, 1.0f);
    values[1].depthStencil = new(1.0f, 0);
    renderPassInfo.clearValueCount = 2;
    renderPassInfo.pClearValues = values;

    vkCmdBeginRenderPass(_commandBuffers[imageIndex], &renderPassInfo, VkSubpassContents.Inline);

    VkViewport viewport = new();
    viewport.x = 0.0f;
    viewport.y = 0.0f;
    viewport.width = _swapchain.Extent2D.width;
    viewport.height = _swapchain.Extent2D.height;
    viewport.minDepth = 0.0f;
    viewport.maxDepth = 1.0f;

    VkRect2D scissor = new(0, 0, _swapchain.Extent2D.width, _swapchain.Extent2D.height);
    vkCmdSetViewport(_commandBuffers[imageIndex], 0, 1, &viewport);
    vkCmdSetScissor(_commandBuffers[imageIndex], 0, 1, &scissor);

    _pipeline.Bind(_commandBuffers[imageIndex]);
    _model.Bind(_commandBuffers[imageIndex]);
    _model.Draw(_commandBuffers[imageIndex]);

    vkCmdEndRenderPass(_commandBuffers[imageIndex]);
    vkEndCommandBuffer(_commandBuffers[imageIndex]).CheckResult();
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
    _pipeline?.Dispose();
    PipelineConfigInfo configInfo = new();
    var pipelineConfig = Pipeline.DefaultConfigInfo(configInfo);
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
  }

  private void DrawFrame() {
    uint imageIndex;
    var result = _swapchain.AcquireNextImage(out imageIndex);

    if (result == VkResult.ErrorOutOfDateKHR) {
      RecreateSwapchain();
      return;
    }

    if (result != VkResult.Success && result != VkResult.SuboptimalKHR) {
      Logger.Error("Failed to acquire next swapchain image");
    }

    GCHandle handle = GCHandle.Alloc(_commandBuffers, GCHandleType.Pinned);
    IntPtr ptr = handle.AddrOfPinnedObject();
    IntPtr elementPtr = new IntPtr(ptr.ToInt64() + imageIndex * Marshal.SizeOf(typeof(VkCommandBuffer)));
    VkCommandBuffer* bufferPtr = (VkCommandBuffer*)elementPtr;

    RecordCommandBuffer((int)imageIndex);
    result = _swapchain.SubmitCommandBuffers(bufferPtr, imageIndex);

    if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || _window.WasWindowResized()) {
      _window.ResetWindowResizedFlag();
      RecreateSwapchain();
      return;
    }

    if (result != VkResult.Success) {
      Logger.Error("Error while submitting Command buffer");
    }
  }

  private void FreeCommandBuffers() {
    if (_commandBuffers != null) {
      for (int i = 0; i < _commandBuffers.Length; i++) {
        vkFreeCommandBuffers(_device.LogicalDevice, _device.CommandPool, _commandBuffers[i]);
      }
      Array.Clear(_commandBuffers);
    }
  }

  private void Cleanup() {
    _model.Dispose();
    FreeCommandBuffers();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
    _swapchain?.Dispose();
    _window?.Dispose();
    _device?.Dispose();
  }
}