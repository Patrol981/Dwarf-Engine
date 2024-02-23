using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.Math;
using Dwarf.Engine.Vulkan;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Dwarf.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public unsafe class Renderer : IDisposable {
  private Window _window = null!;
  // private VulkanDevice _device = null!;
  private IDevice _device;
  private VulkanSwapchain _swapchain = null!;
  private VkCommandBuffer[] _commandBuffers = new VkCommandBuffer[0];

  private uint _imageIndex = 0;
  private int _frameIndex = 0;
  private bool _isFrameStarted = false;

  private CommandList _commandList = null!;
  public Renderer(Window window, VulkanDevice device) {
    _window = window;
    _device = device;

    _commandList = new VulkanCommandList();

    // _swapchain = new Swapchain(_device, _window.Extent);
    RecreateSwapchain();
    // CreateCommandBuffers();
  }

  public VkCommandBuffer BeginFrame(VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    if (_isFrameStarted) {
      Logger.Error("Cannot start frame while already in progress!");
      return VkCommandBuffer.Null;
    }

    var result = _swapchain.AcquireNextImage(out _imageIndex);

    if (result == VkResult.ErrorOutOfDateKHR) {
      RecreateSwapchain();
      return VkCommandBuffer.Null;
    }

    if (result != VkResult.Success && result != VkResult.SuboptimalKHR) {
      Logger.Error("Failed to acquire next swapchain image");
      return VkCommandBuffer.Null;
    }

    _isFrameStarted = true;

    var commandBuffer = GetCurrentCommandBuffer();

    VkCommandBufferBeginInfo beginInfo = new();
    if (level == VkCommandBufferLevel.Secondary) {
      beginInfo.flags = VkCommandBufferUsageFlags.RenderPassContinue;
      // beginInfo.pInheritanceInfo
    }
    // beginInfo.sType = VkStructureType.CommandBufferBeginInfo;

    vkBeginCommandBuffer(commandBuffer, &beginInfo).CheckResult();

    return commandBuffer;
  }

  public void EndFrame() {
    if (!_isFrameStarted) {
      Logger.Error("Cannot end frame is not in progress!");
    }

    var commandBuffer = GetCurrentCommandBuffer();
    vkEndCommandBuffer(commandBuffer).CheckResult();

    var result = _swapchain.SubmitCommandBuffers(&commandBuffer, _imageIndex);

    if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || _window.WasWindowResized()) {
      Logger.Info("Recreate Swapchain Request");
      _window.ResetWindowResizedFlag();
      RecreateSwapchain();
    } else if (result != VkResult.Success) {
      Logger.Error($"Error while submitting Command buffer - {result.ToString()}");
    }

    _isFrameStarted = false;
    // PREV
    _frameIndex = (_frameIndex + 1) % _swapchain.GetMaxFramesInFlight();
    // _frameIndex = (_frameIndex) % _swapchain.GetMaxFramesInFlight();
  }

  public void BeginSwapchainRenderPass(VkCommandBuffer commandBuffer) {
    if (!_isFrameStarted) {
      Logger.Error("Cannot start render pass while already in progress!");
      return;
    }
    if (commandBuffer != GetCurrentCommandBuffer()) {
      Logger.Error("Can't begin render pass on command buffer from diffrent frame!");
      return;
    }

    VkRenderPassBeginInfo renderPassInfo = new();
    renderPassInfo.renderPass = _swapchain.RenderPass;
    renderPassInfo.framebuffer = _swapchain.GetFramebuffer((int)_imageIndex);

    renderPassInfo.renderArea.offset = new VkOffset2D(0, 0);
    renderPassInfo.renderArea.extent = _swapchain.Extent2D;

    VkClearValue[] values = new VkClearValue[2];
    // VkClearValue* values = stackalloc VkClearValue[2];
    values[0].color = new VkClearColorValue(0.01f, 0.01f, 0.01f, 1.0f);
    values[1].depthStencil = new(1.0f, 0);
    fixed (VkClearValue* ptr = values) {
      renderPassInfo.clearValueCount = 2;
      renderPassInfo.pClearValues = ptr;

      vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);
    }

    VkViewport viewport = new();
    viewport.x = 0.0f;
    viewport.y = 0.0f;
    viewport.width = _swapchain.Extent2D.width;
    viewport.height = _swapchain.Extent2D.height;
    viewport.minDepth = 0.0f;
    viewport.maxDepth = 1.0f;

    VkRect2D scissor = new(0, 0, _swapchain.Extent2D.width, _swapchain.Extent2D.height);
    vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
    vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
  }

  public void EndSwapchainRenderPass(VkCommandBuffer commandBuffer) {
    if (!_isFrameStarted) {
      Logger.Error("Cannot end render pass on not started frame!");
      return;
    }
    if (commandBuffer != GetCurrentCommandBuffer()) {
      Logger.Error("Can't end render pass on command buffer from diffrent frame!");
      return;
    }

    vkCmdEndRenderPass(commandBuffer);
  }

  private void RecreateSwapchain() {
    var extent = _window.Extent.ToVkExtent2D();
    while (extent.width == 0 || extent.height == 0 || _window.IsMinimalized) {
      extent = _window.Extent.ToVkExtent2D();
      glfwWaitEvents();
    }

    _device.WaitDevice();

    if (_swapchain != null) _swapchain.Dispose();
    _swapchain = new((VulkanDevice)_device, extent);

    Logger.Info("Recreated Swapchain");

    // if (_swapchain == null) {

    //} else {
    //var copy = _swapchain;
    //_swapchain = new(_device, extent, ref copy);

    //_swapchain?.Dispose();
    //_swapchain = new(_device, extent);

    //if (!copy.CompareSwapFormats(_swapchain)) {
    //Logger.Warn("Swapchain Format has been changed");
    //}

    /*
    if (_commandBuffers == null || _swapchain.ImageCount != _commandBuffers.Length) {
      FreeCommandBuffers();
      CreateCommandBuffers();
    }
    */
    // }
  }

  public void CreateCommandBuffers(VkCommandPool commandPool, VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    int len = _swapchain.GetMaxFramesInFlight();
    _commandBuffers = new VkCommandBuffer[len];

    VkCommandBufferAllocateInfo allocInfo = new();
    allocInfo.level = level;
    allocInfo.commandPool = commandPool;
    allocInfo.commandBufferCount = (uint)_commandBuffers.Length;

    fixed (VkCommandBuffer* ptr = _commandBuffers) {
      vkAllocateCommandBuffers(_device.LogicalDevice, &allocInfo, ptr).CheckResult();
    }
  }

  private void CreateCommandBuffers() {
    CreateCommandBuffers(_device.CommandPool);
  }

  private void FreeCommandBuffers() {
    if (_commandBuffers != null) {
      for (int i = 0; i < _commandBuffers.Length; i++) {
        if (_commandBuffers[i] == VkCommandBuffer.Null) continue;
        vkFreeCommandBuffers(_device.LogicalDevice, _device.CommandPool, _commandBuffers[i]);
      }
      Array.Clear(_commandBuffers);
    }
  }

  public void Dispose() {
    // FreeCommandBuffers();
    _swapchain.Dispose();
  }

  public bool IsFrameInProgress => _isFrameStarted;
  public VkCommandBuffer GetCurrentCommandBuffer() {
    return _commandBuffers[_frameIndex];
  }

  public int GetFrameIndex() {
    return _frameIndex;
  }
  public VkRenderPass GetSwapchainRenderPass() => _swapchain.RenderPass;
  public float AspectRatio => _swapchain.ExtentAspectRatio();
  public DwarfExtent2D Extent2D => _swapchain.Extent2D.FromVkExtent2D();
  public int MAX_FRAMES_IN_FLIGHT => _swapchain.GetMaxFramesInFlight();
  public bool IsFrameStarted => _isFrameStarted;
  public VulkanSwapchain Swapchain => _swapchain;
  public CommandList CommandList => _commandList;
}