using System.Runtime.InteropServices;

using Dwarf.Engine;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public unsafe class Renderer : IDisposable {
  private Window _window = null!;
  private Device _device = null!;
  private Swapchain _swapchain = null!;
  private VkCommandBuffer[] _commandBuffers = new VkCommandBuffer[0];

  private uint _imageIndex = 0;
  private int _frameIndex = 0;
  private bool _isFrameStarted = false;
  public Renderer(Window window, Device device) {
    _window = window;
    _device = device;
    // _swapchain = new Swapchain(_device, _window.Extent);
    RecreateSwapchain();
    CreateCommandBuffers();
  }

  public VkCommandBuffer BeginFrame() {
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
    beginInfo.sType = VkStructureType.CommandBufferBeginInfo;

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
      _window.ResetWindowResizedFlag();
      RecreateSwapchain();
    } else if (result != VkResult.Success) {
      Logger.Error($"Error while submitting Command buffer - {result.ToString()}");
    }

    _isFrameStarted = false;
    _frameIndex = (_frameIndex + 1) % _swapchain.GetMaxFramesInFlight();
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
    renderPassInfo.sType = VkStructureType.RenderPassBeginInfo;
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
    var extent = _window.Extent;
    while (extent.width == 0 || extent.height == 0) {
      extent = _window.Extent;
      glfwWaitEvents();
    }

    vkDeviceWaitIdle(_device.LogicalDevice);

    if (_swapchain != null) _swapchain.Dispose();
    _swapchain = new(_device, extent);

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

  private void CreateCommandBuffers() {
    int len = _swapchain.GetMaxFramesInFlight();
    _commandBuffers = new VkCommandBuffer[len];

    VkCommandBufferAllocateInfo allocInfo = new();
    allocInfo.sType = VkStructureType.CommandBufferAllocateInfo;
    allocInfo.level = VkCommandBufferLevel.Primary;
    allocInfo.commandPool = _device.CommandPool;
    allocInfo.commandBufferCount = (uint)_commandBuffers.Length;

    //GCHandle handle = GCHandle.Alloc(_commandBuffers, GCHandleType.Pinned);
    //IntPtr ptr = handle.AddrOfPinnedObject();
    //VkCommandBuffer* bufferPtr = (VkCommandBuffer*)ptr;

    fixed (VkCommandBuffer* ptr = _commandBuffers) {
      vkAllocateCommandBuffers(_device.LogicalDevice, &allocInfo, ptr).CheckResult();
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

  public void Dispose() {
    FreeCommandBuffers();
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
  public VkExtent2D Extent2D => _swapchain.Extent2D;
  public int MAX_FRAMES_IN_FLIGHT => _swapchain.GetMaxFramesInFlight();
  public bool IsFrameStarted => _isFrameStarted;
}