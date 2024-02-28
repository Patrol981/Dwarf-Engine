using Dwarf.Extensions.Logging;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanSwapchain : IDisposable {
  private const int MAX_FRAMES_IN_FLIGHT = 2;

  private readonly VulkanDevice _device;
  private VkSwapchainKHR _handle = VkSwapchainKHR.Null;
  private VkImageView[] _swapChainImageViews = null!;
  private VkImage[] _swapchainImages = [];
  private VkRenderPass _renderPass = VkRenderPass.Null;
  private VkImage[] _depthImages = [];
  private VkDeviceMemory[] _depthImagesMemories = [];
  private VkImageView[] _depthImageViews = [];
  private VkFormat _swapchainImageFormat = VkFormat.Undefined;
  private VkFormat _swapchainDepthFormat = VkFormat.Undefined;
  private VkExtent2D _swapchainExtent = VkExtent2D.Zero;
  private VkFramebuffer[] _swapchainFramebuffers = [];
  private VkSemaphore[] _imageAvailableSemaphores = [];
  private VkSemaphore[] _renderFinishedSemaphores = [];
  private VkFence[] _inFlightFences = [];
  private VkFence[] _imagesInFlight = [];

  // private Swapchain _oldSwapchain = null!;

  private int _currentFrame = 0;

  private readonly object _swapchainLock = new();

  public VulkanSwapchain(VulkanDevice device, VkExtent2D extent) {
    _device = device;
    Extent2D = extent;

    Init();
  }

  /*
  public Swapchain(Device device, VkExtent2D extent, ref Swapchain previous) {
    _device = device;
    _extent = extent;
    _oldSwapchain = previous;

    Init();

    _oldSwapchain?.Dispose();
    _oldSwapchain = null!;
  }
  */

  public bool CompareSwapFormats(VulkanSwapchain swapchain) {
    return swapchain._swapchainDepthFormat == _swapchainDepthFormat &&
           swapchain._swapchainImageFormat == _swapchainImageFormat;
  }

  private void Init() {
    CreateSwapChain();
    CreateImageViews();
    CreateRenderPass();
    CreateDepthResources();
    CreateFramebuffers();
    CreateSyncObjects();
  }

  private unsafe void CreateSwapChain() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);

    // TODO : Ducktape solution, prevents from crashing
    if (swapChainSupport.Capabilities.maxImageExtent.width < 1)
      swapChainSupport.Capabilities.maxImageExtent.width = Extent2D.width;

    if (swapChainSupport.Capabilities.maxImageExtent.height < 1)
      swapChainSupport.Capabilities.maxImageExtent.height = Extent2D.height;

    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
    var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

    uint imageCount = swapChainSupport.Capabilities.minImageCount + 1;
    if (swapChainSupport.Capabilities.maxImageCount > 0 &&
        imageCount > swapChainSupport.Capabilities.maxImageCount) {
      imageCount = swapChainSupport.Capabilities.maxImageCount;
    }

    var createInfo = new VkSwapchainCreateInfoKHR {
      surface = _device.Surface,

      minImageCount = imageCount,
      imageFormat = surfaceFormat.format,
      imageColorSpace = surfaceFormat.colorSpace,
      imageExtent = extent,
      imageArrayLayers = 1,
      imageUsage = VkImageUsageFlags.ColorAttachment
    };

    var queueFamilies = DeviceHelper.FindQueueFamilies(_device.PhysicalDevice, _device.Surface);

    uint[] indices = new uint[2];

    indices[0] = queueFamilies.graphicsFamily;
    indices[1] = queueFamilies.presentFamily;

    if (queueFamilies.graphicsFamily != queueFamilies.presentFamily) {
      createInfo.imageSharingMode = VkSharingMode.Concurrent;
      createInfo.queueFamilyIndexCount = 2;
      fixed (uint* ptr = indices) {
        createInfo.pQueueFamilyIndices = ptr;
      }
    } else {
      createInfo.imageSharingMode = VkSharingMode.Exclusive;
      createInfo.queueFamilyIndexCount = 0;
      createInfo.pQueueFamilyIndices = null;
    }

    createInfo.preTransform = swapChainSupport.Capabilities.currentTransform;
    createInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque;
    createInfo.presentMode = presentMode;
    createInfo.clipped = true;
    createInfo.oldSwapchain = VkSwapchainKHR.Null;

    /*
    if (_oldSwapchain == null) {
    } else {
      createInfo.oldSwapchain = _oldSwapchain.Handle;
    }
    */

    var result = vkCreateSwapchainKHR(_device.LogicalDevice, &createInfo, null, out _handle);
    if (result != VkResult.Success) throw new Exception("Error while creating swapchain!");

    uint c = imageCount;
    vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle, &c, null);

    VkImage[] imgs = new VkImage[c];
    fixed (VkImage* ptr = imgs) {
      vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle, &c, ptr);
      _swapchainImages = imgs;
    }

    _swapchainImageFormat = surfaceFormat.format;
    _swapchainExtent = extent;

    Logger.Info("Successfully created Swapchain");
  }

  private unsafe void CreateImageViews() {
    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);
    _swapChainImageViews = new VkImageView[swapChainImages.Length];

    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
    var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

    for (int i = 0; i < swapChainImages.Length; i++) {
      var viewCreateInfo = new VkImageViewCreateInfo(
          swapChainImages[i],
          VkImageViewType.Image2D,
          surfaceFormat.format,
          VkComponentMapping.Rgba,
          new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1)
          );

      vkCreateImageView(_device.LogicalDevice, &viewCreateInfo, null, out _swapChainImageViews[i]).CheckResult();
    }
  }

  private unsafe void CreateRenderPass() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);

    VkAttachmentDescription depthAttachment = new() {
      format = FindDepthFormat(),
      samples = VkSampleCountFlags.Count1,
      loadOp = VkAttachmentLoadOp.Clear,
      storeOp = VkAttachmentStoreOp.DontCare,
      stencilLoadOp = VkAttachmentLoadOp.DontCare,
      stencilStoreOp = VkAttachmentStoreOp.DontCare,
      initialLayout = VkImageLayout.Undefined,
      finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
    };

    VkAttachmentReference depthAttachmentRef = new() {
      attachment = 1,
      layout = VkImageLayout.DepthStencilAttachmentOptimal
    };

    VkAttachmentDescription colorAttachment = new() {
      format = surfaceFormat.format,
      samples = VkSampleCountFlags.Count1,
      loadOp = VkAttachmentLoadOp.Clear,
      storeOp = VkAttachmentStoreOp.Store,
      stencilStoreOp = VkAttachmentStoreOp.DontCare,
      stencilLoadOp = VkAttachmentLoadOp.DontCare,
      initialLayout = VkImageLayout.Undefined,
      finalLayout = VkImageLayout.PresentSrcKHR
    };

    VkAttachmentReference colorAttachmentRef = new() {
      attachment = 0,
      layout = VkImageLayout.ColorAttachmentOptimal
    };

    VkSubpassDescription subpass = new() {
      pipelineBindPoint = VkPipelineBindPoint.Graphics,
      colorAttachmentCount = 1,
      pColorAttachments = &colorAttachmentRef,
      pDepthStencilAttachment = &depthAttachmentRef
    };

    VkSubpassDependency dependency = new() {
      srcSubpass = VK_SUBPASS_EXTERNAL,
      srcAccessMask = 0,
      srcStageMask =
        VkPipelineStageFlags.ColorAttachmentOutput |
        VkPipelineStageFlags.EarlyFragmentTests,
      dstSubpass = 0,
      dstStageMask =
        VkPipelineStageFlags.ColorAttachmentOutput |
        VkPipelineStageFlags.EarlyFragmentTests,
      dstAccessMask =
        VkAccessFlags.ColorAttachmentWrite |
        VkAccessFlags.DepthStencilAttachmentWrite
    };

    VkAttachmentDescription[] attachments = [colorAttachment, depthAttachment];
    VkRenderPassCreateInfo renderPassInfo = new() {
      attachmentCount = 2
    };
    fixed (VkAttachmentDescription* ptr = attachments) {
      renderPassInfo.pAttachments = ptr;
    }
    renderPassInfo.subpassCount = 1;
    renderPassInfo.pSubpasses = &subpass;
    renderPassInfo.dependencyCount = 1;
    renderPassInfo.pDependencies = &dependency;

    var result = vkCreateRenderPass(_device.LogicalDevice, &renderPassInfo, null, out _renderPass);
    if (result != VkResult.Success) throw new Exception("Failed to create render pass!");
  }

  private unsafe void CreateDepthResources() {
    var depthFormat = FindDepthFormat();
    _swapchainDepthFormat = depthFormat;

    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);

    _depthImages = new VkImage[swapChainImages.Length];
    _depthImagesMemories = new VkDeviceMemory[swapChainImages.Length];
    _depthImageViews = new VkImageView[swapChainImages.Length];

    for (int i = 0; i < _depthImages.Length; i++) {
      VkImageCreateInfo imageInfo = new() {
        imageType = VkImageType.Image2D
      };
      imageInfo.extent.width = _swapchainExtent.width;
      imageInfo.extent.height = _swapchainExtent.height;
      imageInfo.extent.depth = 1;
      imageInfo.mipLevels = 1;
      imageInfo.arrayLayers = 1;
      imageInfo.format = depthFormat;
      imageInfo.tiling = VkImageTiling.Optimal;
      imageInfo.initialLayout = VkImageLayout.Undefined;
      imageInfo.usage = VkImageUsageFlags.DepthStencilAttachment;
      imageInfo.samples = VkSampleCountFlags.Count1;
      imageInfo.sharingMode = VkSharingMode.Exclusive;
      imageInfo.flags = 0;

      _device.CreateImageWithInfo(imageInfo, VkMemoryPropertyFlags.DeviceLocal, out _depthImages[i], out _depthImagesMemories[i]);

      VkImageViewCreateInfo viewInfo = new() {
        image = _depthImages[i],
        viewType = VkImageViewType.Image2D,
        format = depthFormat
      };
      viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Depth;
      viewInfo.subresourceRange.baseMipLevel = 0;
      viewInfo.subresourceRange.levelCount = 1;
      viewInfo.subresourceRange.baseArrayLayer = 0;
      viewInfo.subresourceRange.layerCount = 1;

      vkCreateImageView(_device.LogicalDevice, &viewInfo, null, out _depthImageViews[i]).CheckResult();
    }
  }

  private unsafe void CreateFramebuffers() {
    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);
    _swapchainFramebuffers = new VkFramebuffer[swapChainImages.Length];

    for (int i = 0; i < swapChainImages.Length; i++) {
      VkImageView[] attachmetns = [_swapChainImageViews[i], _depthImageViews[i]];
      fixed (VkImageView* ptr = attachmetns) {
        VkFramebufferCreateInfo framebufferInfo = new() {
          renderPass = _renderPass,
          attachmentCount = 2,
          pAttachments = ptr,
          width = _swapchainExtent.width,
          height = _swapchainExtent.height,
          layers = 1
        };

        vkCreateFramebuffer(_device.LogicalDevice, &framebufferInfo, null, out _swapchainFramebuffers[i]).CheckResult();
      }
    }
  }

  private unsafe void CreateSyncObjects() {
    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);
    _imageAvailableSemaphores = new VkSemaphore[MAX_FRAMES_IN_FLIGHT];
    _renderFinishedSemaphores = new VkSemaphore[MAX_FRAMES_IN_FLIGHT];
    _inFlightFences = new VkFence[MAX_FRAMES_IN_FLIGHT];
    _imagesInFlight = new VkFence[swapChainImages.Length];
    Array.Fill(_imagesInFlight, VkFence.Null);

    VkSemaphoreCreateInfo semaphoreInfo = new();

    VkFenceCreateInfo fenceInfo = new() {
      flags = VkFenceCreateFlags.Signaled
    };

    for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++) {
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreInfo, null, out _imageAvailableSemaphores[i]).CheckResult();
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreInfo, null, out _renderFinishedSemaphores[i]).CheckResult();
      vkCreateFence(_device.LogicalDevice, &fenceInfo, null, out _inFlightFences[i]).CheckResult();
    }
  }

  public unsafe VkResult AcquireNextImage(out uint imageIndex) {
    fixed (VkFence* fencePtr = _inFlightFences) {
      vkWaitForFences(_device.LogicalDevice, (uint)_inFlightFences.Length, fencePtr, true, ulong.MaxValue);
    }

    VkResult result = vkAcquireNextImageKHR(
      _device.LogicalDevice,
      _handle,
      ulong.MaxValue,
      _imageAvailableSemaphores[_currentFrame],
      VkFence.Null,
      out imageIndex
    );

    return result;
  }

  public unsafe VkResult SubmitCommandBuffers(VkCommandBuffer* buffers, uint imageIndex) {
    lock (_swapchainLock) {
      if (_imagesInFlight[imageIndex] != VkFence.Null) {
        vkWaitForFences(_device.LogicalDevice, _inFlightFences, true, ulong.MaxValue);
      }
      _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];

      // var waitStages = new VkPipelineStageFlags[1];
      // waitStages[0] = VkPipelineStageFlags.ColorAttachmentOutput;

      VkSubmitInfo submitInfo = new();

      VkSemaphore* waitSemaphores = stackalloc VkSemaphore[1];
      waitSemaphores[0] = _imageAvailableSemaphores[_currentFrame];

      VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[1];
      waitStages[0] = VkPipelineStageFlags.ColorAttachmentOutput;

      submitInfo.waitSemaphoreCount = 1;
      // fixed (VkSemaphore* waitSemaphoresPtr = _imageAvailableSemaphores)
      //fixed (VkPipelineStageFlags* waitStagesPtr = waitStages) {
      submitInfo.pWaitSemaphores = waitSemaphores;
      submitInfo.pWaitDstStageMask = waitStages;
      // submitInfo.pWaitDstStageMask = null;
      // }

      submitInfo.commandBufferCount = 1;
      submitInfo.pCommandBuffers = buffers;

      VkSwapchainKHR[] swapchains = [_handle];
      VkSemaphore[] signalSemaphores = [_renderFinishedSemaphores[_currentFrame]];

      //var fenceInfo = new VkFenceCreateInfo();
      //fenceInfo.flags = VkFenceCreateFlags.None;
      //vkCreateFence(_device.LogicalDevice, &fenceInfo, null, out var fence).CheckResult();

      fixed (VkSwapchainKHR* swPtr = swapchains)
      fixed (VkFence* swFlightFencesPtr = _inFlightFences)
      fixed (VkSemaphore* signalPtr = signalSemaphores) {
        submitInfo.signalSemaphoreCount = 1;
        submitInfo.pSignalSemaphores = signalPtr;

        // _device._mutex.WaitOne();
        // vkWaitForFences(_device.LogicalDevice, 1, &fence, VkBool32.True, 100000000000);
        // vkWaitForFences(_device.LogicalDevice, 1, swFlightFencesPtr, VkBool32.True, 100000000000);
        vkResetFences(_device.LogicalDevice, _inFlightFences[_currentFrame]);
        _device._mutex.WaitOne();
        try {
          vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _inFlightFences[_currentFrame]);
        } finally {
          _device._mutex.ReleaseMutex();
        }
        // vkDestroyFence(_device.LogicalDevice, fence, null);
        // _device._mutex.ReleaseMutex();

        VkPresentInfoKHR presentInfo = new() {
          waitSemaphoreCount = 1,
          pWaitSemaphores = signalPtr
        };

        presentInfo.swapchainCount = 1;
        presentInfo.pSwapchains = swPtr;

        presentInfo.pImageIndices = &imageIndex;

        var result = vkQueuePresentKHR(_device.PresentQueue, &presentInfo);
        _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;

        return result;
      }
    }
  }

  private VkFormat FindDepthFormat() {
    var items = new List<VkFormat> {
      VkFormat.D32Sfloat,
      VkFormat.D32SfloatS8Uint,
      VkFormat.D24UnormS8Uint
    };
    return _device.FindSupportedFormat(items, VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
  }

  private VkSurfaceFormatKHR ChooseSwapSurfaceFormat(ReadOnlySpan<VkSurfaceFormatKHR> availableFormats) {
    // If the surface format list only includes one entry with VK_FORMAT_UNDEFINED,
    // there is no preferred format, so we assume VK_FORMAT_B8G8R8A8_UNORM
    if ((availableFormats.Length == 1) && (availableFormats[0].format == VkFormat.Undefined)) {
      return new VkSurfaceFormatKHR(VkFormat.R8G8B8A8Unorm, availableFormats[0].colorSpace);
    }

    // iterate over the list of available surface format and
    // check for the presence of VK_FORMAT_B8G8R8A8_UNORM
    foreach (VkSurfaceFormatKHR availableFormat in availableFormats) {
      // R8G8B8A8Unorm
      // R8G8B8A8Srgb
      if (availableFormat.format == VkFormat.R8G8B8A8Unorm) {
        return availableFormat;
      }
    }

    return availableFormats[0];
  }

  private VkPresentModeKHR ChooseSwapPresentMode(ReadOnlySpan<VkPresentModeKHR> availablePresentModes) {
    foreach (VkPresentModeKHR availablePresentMode in availablePresentModes) {
      // render mode
      if (availablePresentMode == VkPresentModeKHR.Mailbox || availablePresentMode == VkPresentModeKHR.Immediate) {
        return availablePresentMode;
      }
    }

    return VkPresentModeKHR.Fifo;
  }

  private VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities) {
    if (capabilities.currentExtent.width > 0) {
      return capabilities.currentExtent;
    } else {
      VkExtent2D actualExtent = Extent2D;

      actualExtent = new VkExtent2D(
        Math.Max(capabilities.minImageExtent.width, Math.Min(capabilities.maxImageExtent.width, actualExtent.width)),
        Math.Max(capabilities.minImageExtent.height, Math.Min(capabilities.maxImageExtent.height, actualExtent.height))
      );

      return actualExtent;
    }
  }

  public unsafe void Dispose() {
    for (int i = 0; i < _swapChainImageViews.Length; i++) {
      vkDestroyImageView(_device.LogicalDevice, _swapChainImageViews[i]);
    }
    for (int i = 0; i < _depthImages.Length; i++) {
      vkDestroyImage(_device.LogicalDevice, _depthImages[i]);
    }
    for (int i = 0; i < _depthImagesMemories.Length; i++) {
      vkFreeMemory(_device.LogicalDevice, _depthImagesMemories[i]);
    }
    for (int i = 0; i < _depthImageViews.Length; i++) {
      vkDestroyImageView(_device.LogicalDevice, _depthImageViews[i]);
    }
    for (int i = 0; i < _swapchainFramebuffers.Length; i++) {
      vkDestroyFramebuffer(_device.LogicalDevice, _swapchainFramebuffers[i]);
    }
    for (int i = 0; i < _imageAvailableSemaphores.Length; i++) {
      vkDestroySemaphore(_device.LogicalDevice, _imageAvailableSemaphores[i]);
    }
    for (int i = 0; i < _renderFinishedSemaphores.Length; i++) {
      vkDestroySemaphore(_device.LogicalDevice, _renderFinishedSemaphores[i]);
    }
    for (int i = 0; i < _inFlightFences.Length; i++) {
      if (_inFlightFences[i] != VkFence.Null)
        vkDestroyFence(_device.LogicalDevice, _inFlightFences[i]);
    }
    for (int i = 0; i < _imagesInFlight.Length; i++) {
      //if (_imagesInFlight[i] != VkFence.Null)
      //vkDestroyFence(_device.LogicalDevice, _imagesInFlight[i]);
    }



    vkDestroyRenderPass(_device.LogicalDevice, _renderPass);
    vkDestroySwapchainKHR(_device.LogicalDevice, _handle);

    for (int i = 0; i < _swapchainImages.Length; i++) {
      // vkDestroyImage(_device.LogicalDevice, _swapchainImages[i]);
    }
  }

  private uint GetImageCount() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    uint imageCount = swapChainSupport.Capabilities.minImageCount + 1;
    if (swapChainSupport.Capabilities.maxImageCount > 0 &&
        imageCount > swapChainSupport.Capabilities.maxImageCount) {
      imageCount = swapChainSupport.Capabilities.maxImageCount;
    }
    return imageCount;
  }

  public VkFramebuffer GetFramebuffer(int index) {
    return _swapchainFramebuffers[index];
  }

  public VkSwapchainKHR Handle => _handle;
  public VkExtent2D Extent2D { get; }
  public VkRenderPass RenderPass => _renderPass;
  public uint ImageCount => GetImageCount();
  public int GetMaxFramesInFlight() => MAX_FRAMES_IN_FLIGHT;
  public float ExtentAspectRatio() {
    return _swapchainExtent.width / _swapchainExtent.height;
  }
}