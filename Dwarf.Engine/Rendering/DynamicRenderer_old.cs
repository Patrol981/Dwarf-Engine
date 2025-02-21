using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Vulkan;
using Dwarf.Windowing;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public unsafe class DynamicRendererOld : IRenderer {
  private readonly Window _window = null!;
  private readonly IDevice _device;
  private VkCommandBuffer[] _commandBuffers = [];

  private uint _imageIndex = 0;
  private int _frameIndex = 0;

  public DynamicRendererOld(Window window, VulkanDevice device) {
    _window = window;
    _device = device;

    CommandList = new VulkanCommandList();

    RecreateSwapchain();
  }

  public VkCommandBuffer BeginFrame(VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    if (IsFrameInProgress) {
      Logger.Error("Cannot start frame while already in progress!");
      return VkCommandBuffer.Null;
    }

    var result = Swapchain.AcquireNextImage(out _imageIndex);

    if (result == VkResult.ErrorOutOfDateKHR) {
      RecreateSwapchain();
      return VkCommandBuffer.Null;
    }

    if (result != VkResult.Success && result != VkResult.SuboptimalKHR) {
      Logger.Error("Failed to acquire next swapchain image");
      return VkCommandBuffer.Null;
    }

    IsFrameInProgress = true;

    var commandBuffer = CurrentCommandBuffer;

    vkResetCommandBuffer(commandBuffer, VkCommandBufferResetFlags.None);

    VkCommandBufferBeginInfo beginInfo = new();
    if (level == VkCommandBufferLevel.Secondary) {
      beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUse;
    }

    vkBeginCommandBuffer(commandBuffer, &beginInfo).CheckResult();

    VkUtils.InsertMemoryBarrier(
      commandBuffer,
      Swapchain.CurrentImageColor,
      0,
      VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
      VK_IMAGE_LAYOUT_UNDEFINED,
      VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
      VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
      VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
      new(VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1)
    );

    VkUtils.InsertMemoryBarrier(
      commandBuffer,
      Swapchain.CurrentImageDepth,
      0,
      VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
      VK_IMAGE_LAYOUT_UNDEFINED,
      VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
      VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
      VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
      new(VK_IMAGE_ASPECT_DEPTH_BIT, 0, 1, 0, 1)
    );

    return commandBuffer;
  }

  public void EndFrame() {
    if (!IsFrameInProgress) {
      Logger.Error("Cannot end frame is not in progress!");
    }

    var commandBuffer = CurrentCommandBuffer;

    VkUtils.InsertMemoryBarrier(
      commandBuffer,
      Swapchain.GetImageColor(_imageIndex),
      VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
      0,
      VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
      VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
      VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
      VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
      new(VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1)
    );

    vkEndCommandBuffer(commandBuffer).CheckResult();

    var result = Swapchain.SubmitCommandBuffers(&commandBuffer, _imageIndex);

    if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || _window.WasWindowResized()) {
      Logger.Info("Recreate Swapchain Request");
      _window.ResetWindowResizedFlag();
      RecreateSwapchain();
    } else if (result != VkResult.Success) {
      Logger.Error($"Error while submitting Command buffer - {result.ToString()}");
    }

    IsFrameInProgress = false;
    _frameIndex = (_frameIndex + 1) % Swapchain.GetMaxFramesInFlight();
  }

  public void BeginRendering(VkCommandBuffer commandBuffer) {
    if (!IsFrameInProgress) {
      Logger.Error("Cannot start render pass while already in progress!");
      return;
    }
    if (commandBuffer != CurrentCommandBuffer) {
      Logger.Error("Can't begin render pass on command buffer from diffrent frame!");
      return;
    }

    VkClearValue[] clearValues = new VkClearValue[2];
    clearValues[0].color = new VkClearColorValue(0.0f, 0.0f, 0.0f, 0.0f);
    clearValues[1].depthStencil = new(1.0f, 0);

    VkRenderingAttachmentInfo colorAttachmentInfo = new() {
      imageView = Swapchain.CurrentImageColorView,
      imageLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
      loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR,
      storeOp = VK_ATTACHMENT_STORE_OP_STORE,
      clearValue = clearValues[0]
    };

    VkRenderingAttachmentInfo depthAttachmentInfo = new() {
      imageView = Swapchain.GetImageDepthView(_imageIndex),
      imageLayout = VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
      loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR,
      storeOp = VK_ATTACHMENT_STORE_OP_STORE,
      clearValue = clearValues[1]
    };

    VkViewport viewport = new() {
      x = 0.0f,
      y = 0.0f,
      width = Swapchain.Extent2D.width,
      height = Swapchain.Extent2D.height,
      minDepth = 0.0f,
      maxDepth = 1.0f
    };
    VkRect2D scissor = new(0, 0, Swapchain.Extent2D.width, Swapchain.Extent2D.height);

    VkRenderingInfo renderingInfo = new() {
      renderArea = new(0, 0, Swapchain.Extent2D.width, Swapchain.Extent2D.height),
      layerCount = 1,
      colorAttachmentCount = 1,
      pColorAttachments = &colorAttachmentInfo,
      pDepthAttachment = &depthAttachmentInfo,
      pStencilAttachment = null,
    };

    vkCmdBeginRendering(commandBuffer, &renderingInfo);

    vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
    vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
  }

  public void EndRendering(VkCommandBuffer commandBuffer) {
    if (!IsFrameInProgress) {
      Logger.Error("Cannot end render pass on not started frame!");
      return;
    }
    if (commandBuffer != CurrentCommandBuffer) {
      Logger.Error("Can't end render pass on command buffer from diffrent frame!");
      return;
    }

    vkCmdEndRendering(commandBuffer);
  }

  public void RecreateSwapchain() {
    var extent = _window.Extent.ToVkExtent2D();
    while (extent.width == 0 || extent.height == 0 || _window.IsMinimalized) {
      extent = _window.Extent.ToVkExtent2D();
    }

    _device.WaitDevice();

    Swapchain?.Dispose();
    Swapchain = new((VulkanDevice)_device, extent);

    Logger.Info("Recreated Swapchain");
  }

  public void CreateCommandBuffers(VkCommandPool commandPool, VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    int len = Swapchain.GetMaxFramesInFlight();
    _commandBuffers = new VkCommandBuffer[len];

    VkCommandBufferAllocateInfo allocInfo = new();
    allocInfo.level = level;
    allocInfo.commandPool = commandPool;
    allocInfo.commandBufferCount = (uint)_commandBuffers.Length;

    fixed (VkCommandBuffer* ptr = _commandBuffers) {
      vkAllocateCommandBuffers(_device.LogicalDevice, &allocInfo, ptr).CheckResult();
    }
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public VkRenderPass GetSwapchainRenderPass() {
    return VkRenderPass.Null;
  }

  public VkRenderPass GetPostProcessingPass() {
    return VkRenderPass.Null;
  }

  public VkCommandBuffer CurrentCommandBuffer => _commandBuffers[_frameIndex];
  public int FrameIndex => _frameIndex;
  public int ImageIndex => (int)_imageIndex;
  public bool IsFrameInProgress { get; private set; } = false;
  public float AspectRatio => Swapchain.ExtentAspectRatio();
  public DwarfExtent2D Extent2D => Swapchain.Extent2D.FromVkExtent2D();
  public int MAX_FRAMES_IN_FLIGHT => Swapchain.GetMaxFramesInFlight();
  public bool IsFrameStarted => IsFrameInProgress;
  public VulkanSwapchain Swapchain { get; private set; } = null!;
  public CommandList CommandList { get; } = null!;

  public VulkanDynamicSwapchain DynamicSwapchain => throw new NotImplementedException();

  public VkFormat DepthFormat => throw new NotImplementedException();

  public void UpdateDescriptors() {
    throw new NotImplementedException();
  }

  public VkDescriptorSet PostProcessDecriptor => throw new NotImplementedException();

  public VkDescriptorSet PreviousPostProcessDescriptor => throw new NotImplementedException();
}