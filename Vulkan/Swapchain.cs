using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Dwarf.Engine.Windowing;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class Swapchain : IDisposable {
  private const int MAX_FRAMES_IN_FLIGHT = 2;

  private readonly Device _device;
  private readonly VkExtent2D _extent;

  private VkSwapchainKHR _handle = VkSwapchainKHR.Null;
  private VkImageView[] _swapChainImageViews = null!;
  // private unsafe VkImage* _swapchainImages;
  private VkImage[] _swapchainImages = new VkImage[0];
  private VkRenderPass _renderPass = VkRenderPass.Null;
  private VkImage[] _depthImages = new VkImage[0];
  private VkDeviceMemory[] _depthImagesMemories = new VkDeviceMemory[0];
  private VkImageView[] _depthImageViews = new VkImageView[0];
  private VkFormat _swapchainImageFormat = VkFormat.Undefined;
  private VkFormat _swapchainDepthFormat = VkFormat.Undefined;
  private VkExtent2D _swapchainExtent = VkExtent2D.Zero;
  private VkFramebuffer[] _swapchainFramebuffers = new VkFramebuffer[0];
  private VkSemaphore[] _imageAvailableSemaphores = new VkSemaphore[0];
  private VkSemaphore[] _renderFinishedSemaphores = new VkSemaphore[0];
  private VkFence[] _inFlightFences = new VkFence[0];
  private VkFence[] _imagesInFlight = new VkFence[0];

  // private Swapchain _oldSwapchain = null!;

  private int _currentFrame = 0;

  public Swapchain(Device device, VkExtent2D extent) {
    _device = device;
    _extent = extent;

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

  public bool CompareSwapFormats(Swapchain swapchain) {
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
    SwapChainSupportDetails swapChainSupport = Utils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);

    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
    var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

    uint imageCount = swapChainSupport.Capabilities.minImageCount + 1;
    if (swapChainSupport.Capabilities.maxImageCount > 0 &&
        imageCount > swapChainSupport.Capabilities.maxImageCount) {
      imageCount = swapChainSupport.Capabilities.maxImageCount;
    }

    var createInfo = new VkSwapchainCreateInfoKHR();
    createInfo.sType = VkStructureType.SwapchainCreateInfoKHR;
    createInfo.surface = _device.Surface;

    createInfo.minImageCount = imageCount;
    createInfo.imageFormat = surfaceFormat.format;
    createInfo.imageColorSpace = surfaceFormat.colorSpace;
    createInfo.imageExtent = extent;
    createInfo.imageArrayLayers = 1;
    createInfo.imageUsage = VkImageUsageFlags.ColorAttachment;

    var queueFamilies = DeviceHelper.FindQueueFamilies(_device.PhysicalDevice, _device.Surface);

    uint[] indices = new uint[2];
    // uint* indices = stackalloc uint[2];
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

    int c = (int)imageCount;
    vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle, &c, null);
    // var imgs = stackalloc VkImage[c];
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

    SwapChainSupportDetails swapChainSupport = Utils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
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
    SwapChainSupportDetails swapChainSupport = Utils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);

    VkAttachmentDescription depthAttachment = new();
    depthAttachment.format = FindDepthFormat();
    depthAttachment.samples = VkSampleCountFlags.Count1;
    depthAttachment.loadOp = VkAttachmentLoadOp.Clear;
    depthAttachment.storeOp = VkAttachmentStoreOp.DontCare;
    depthAttachment.stencilLoadOp = VkAttachmentLoadOp.DontCare;
    depthAttachment.stencilStoreOp = VkAttachmentStoreOp.DontCare;
    depthAttachment.initialLayout = VkImageLayout.Undefined;
    depthAttachment.finalLayout = VkImageLayout.DepthStencilAttachmentOptimal;

    VkAttachmentReference depthAttachmentRef = new();
    depthAttachmentRef.attachment = 1;
    depthAttachmentRef.layout = VkImageLayout.DepthStencilAttachmentOptimal;

    VkAttachmentDescription colorAttachment = new();
    colorAttachment.format = surfaceFormat.format;
    colorAttachment.samples = VkSampleCountFlags.Count1;
    colorAttachment.loadOp = VkAttachmentLoadOp.Clear;
    colorAttachment.storeOp = VkAttachmentStoreOp.Store;
    colorAttachment.stencilStoreOp = VkAttachmentStoreOp.DontCare;
    colorAttachment.stencilLoadOp = VkAttachmentLoadOp.DontCare;
    colorAttachment.initialLayout = VkImageLayout.Undefined;
    colorAttachment.finalLayout = VkImageLayout.PresentSrcKHR;

    VkAttachmentReference colorAttachmentRef = new();
    colorAttachmentRef.attachment = 0;
    colorAttachmentRef.layout = VkImageLayout.ColorAttachmentOptimal;

    VkSubpassDescription subpass = new();
    subpass.pipelineBindPoint = VkPipelineBindPoint.Graphics;
    subpass.colorAttachmentCount = 1;
    subpass.pColorAttachments = &colorAttachmentRef;
    subpass.pDepthStencilAttachment = &depthAttachmentRef;

    VkSubpassDependency dependency = new();
    dependency.srcSubpass = VK_SUBPASS_EXTERNAL;
    dependency.srcAccessMask = 0;
    dependency.srcStageMask =
      VkPipelineStageFlags.ColorAttachmentOutput |
      VkPipelineStageFlags.EarlyFragmentTests;
    dependency.dstSubpass = 0;
    dependency.dstStageMask =
      VkPipelineStageFlags.ColorAttachmentOutput |
      VkPipelineStageFlags.EarlyFragmentTests;
    dependency.dstAccessMask =
      VkAccessFlags.ColorAttachmentWrite |
      VkAccessFlags.DepthStencilAttachmentWrite;

    // VkAttachmentDescription* attachments = stackalloc VkAttachmentDescription[2];
    VkAttachmentDescription[] attachments = new VkAttachmentDescription[2];
    attachments[0] = colorAttachment;
    attachments[1] = depthAttachment;
    VkRenderPassCreateInfo renderPassInfo = new();
    renderPassInfo.sType = VkStructureType.RenderPassCreateInfo;
    renderPassInfo.attachmentCount = 2;
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
      VkImageCreateInfo imageInfo = new();
      imageInfo.sType = VkStructureType.ImageCreateInfo;
      imageInfo.imageType = VkImageType.Image2D;
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

      VkImageViewCreateInfo viewInfo = new();
      viewInfo.sType = VkStructureType.ImageViewCreateInfo;
      viewInfo.image = _depthImages[i];
      viewInfo.viewType = VkImageViewType.Image2D;
      viewInfo.format = depthFormat;
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
      VkImageView[] attachmetns = new VkImageView[2];
      attachmetns[0] = _swapChainImageViews[i];
      attachmetns[1] = _depthImageViews[i];

      fixed (VkImageView* ptr = attachmetns) {
        VkFramebufferCreateInfo framebufferInfo = new();
        framebufferInfo.sType = VkStructureType.FramebufferCreateInfo;
        framebufferInfo.renderPass = _renderPass;
        framebufferInfo.attachmentCount = 2;
        framebufferInfo.pAttachments = ptr;
        framebufferInfo.width = _swapchainExtent.width;
        framebufferInfo.height = _swapchainExtent.height;
        framebufferInfo.layers = 1;

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
    semaphoreInfo.sType = VkStructureType.SemaphoreCreateInfo;

    VkFenceCreateInfo fenceInfo = new();
    fenceInfo.sType = VkStructureType.FenceCreateInfo;
    fenceInfo.flags = VkFenceCreateFlags.Signaled;

    for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++) {
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreInfo, null, out _imageAvailableSemaphores[i]).CheckResult();
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreInfo, null, out _renderFinishedSemaphores[i]).CheckResult();
      vkCreateFence(_device.LogicalDevice, &fenceInfo, null, out _inFlightFences[i]).CheckResult();
    }
  }

  public unsafe VkResult AcquireNextImage(out uint imageIndex) {
    GCHandle handle = GCHandle.Alloc(_inFlightFences, GCHandleType.Pinned);
    IntPtr ptr = handle.AddrOfPinnedObject();
    VkFence* fencePtr = (VkFence*)ptr;

    /*
    vkWaitForFences(
      _device.LogicalDevice,
      1,
      fencePtr,
      true,
      ulong.MaxValue
    ).CheckResult();
    */

    vkWaitForFences(_device.LogicalDevice, _inFlightFences, true, ulong.MaxValue).CheckResult();

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
    if (_imagesInFlight[imageIndex] != VkFence.Null) {
      vkWaitForFences(_device.LogicalDevice, _inFlightFences, true, ulong.MaxValue);
    }
    _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];

    VkSubmitInfo submitInfo = new();
    submitInfo.sType = VkStructureType.SubmitInfo;

    // VkSemaphore* waitSemaphores = stackalloc VkSemaphore[1];
    VkSemaphore[] waitSemaphores = new VkSemaphore[1];
    waitSemaphores[0] = _imageAvailableSemaphores[_currentFrame];

    // VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[1];
    VkPipelineStageFlags[] waitStages = new VkPipelineStageFlags[1];
    waitStages[0] = VkPipelineStageFlags.ColorAttachmentOutput;
    submitInfo.waitSemaphoreCount = 1;
    fixed (VkSemaphore* ptr = waitSemaphores) {
      submitInfo.pWaitSemaphores = ptr;
    }
    fixed (VkPipelineStageFlags* ptr = waitStages) {
      submitInfo.pWaitDstStageMask = ptr;
    }

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = buffers;

    // VkSemaphore* signalSemaphores = stackalloc VkSemaphore[1];
    VkSemaphore[] signalSemaphores = new VkSemaphore[1];
    signalSemaphores[0] = _renderFinishedSemaphores[_currentFrame];
    fixed (VkSemaphore* signalPtr = signalSemaphores) {
      submitInfo.signalSemaphoreCount = 1;
      submitInfo.pSignalSemaphores = signalPtr;

      vkResetFences(_device.LogicalDevice, _inFlightFences[_currentFrame]);
      vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _inFlightFences[_currentFrame]).CheckResult();

      VkPresentInfoKHR presentInfo = new();
      presentInfo.sType = VkStructureType.PresentInfoKHR;

      presentInfo.waitSemaphoreCount = 1;
      presentInfo.pWaitSemaphores = signalPtr;

      //VkSwapchainKHR* swapchains = stackalloc VkSwapchainKHR[1];
      VkSwapchainKHR[] swapchains = new VkSwapchainKHR[1];
      swapchains[0] = _handle;
      presentInfo.swapchainCount = 1;
      fixed (VkSwapchainKHR* ptr = swapchains) {
        presentInfo.pSwapchains = ptr;
      }

      presentInfo.pImageIndices = &imageIndex;

      var result = vkQueuePresentKHR(_device.PresentQueue, &presentInfo);
      _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;

      return result;
    }
  }

  private VkFormat FindDepthFormat() {
    var items = new List<VkFormat>();
    items.Add(VkFormat.D32Sfloat);
    items.Add(VkFormat.D32SfloatS8Uint);
    items.Add(VkFormat.D24UnormS8Uint);
    return _device.FindSupportedFormat(items, VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
  }

  private VkSurfaceFormatKHR ChooseSwapSurfaceFormat(ReadOnlySpan<VkSurfaceFormatKHR> availableFormats) {
    // If the surface format list only includes one entry with VK_FORMAT_UNDEFINED,
    // there is no preferred format, so we assume VK_FORMAT_B8G8R8A8_UNORM
    if ((availableFormats.Length == 1) && (availableFormats[0].format == VkFormat.Undefined)) {
      return new VkSurfaceFormatKHR(VkFormat.B8G8R8A8Unorm, availableFormats[0].colorSpace);
    }

    // iterate over the list of available surface format and
    // check for the presence of VK_FORMAT_B8G8R8A8_UNORM
    foreach (VkSurfaceFormatKHR availableFormat in availableFormats) {
      // B8G8R8A8Unorm
      if (availableFormat.format == VkFormat.B8G8R8A8Srgb) {
        return availableFormat;
      }
    }

    return availableFormats[0];
  }

  private VkPresentModeKHR ChooseSwapPresentMode(ReadOnlySpan<VkPresentModeKHR> availablePresentModes) {
    foreach (VkPresentModeKHR availablePresentMode in availablePresentModes) {
      if (availablePresentMode == VkPresentModeKHR.Mailbox) {
        return availablePresentMode;
      }
    }

    return VkPresentModeKHR.Fifo;
  }

  private VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities) {
    if (capabilities.currentExtent.width > 0) {
      return capabilities.currentExtent;
    } else {
      VkExtent2D actualExtent = _extent;

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
    SwapChainSupportDetails swapChainSupport = Utils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
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
  public VkExtent2D Extent2D => _extent;
  public VkRenderPass RenderPass => _renderPass;
  public uint ImageCount => GetImageCount();
  public int GetMaxFramesInFlight() => MAX_FRAMES_IN_FLIGHT;
  public float ExtentAspectRatio() {
    return _swapchainExtent.width / _swapchainExtent.height;
  }
}