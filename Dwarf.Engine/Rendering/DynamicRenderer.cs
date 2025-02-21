using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Dwarf.Windowing;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public unsafe class DynamicRenderer : IRenderer {
  private readonly Window _window = null!;
  private readonly VulkanDevice _device;
  private VkCommandBuffer[] _commandBuffers = [];
  private DescriptorPool _descriptorPool = null!;
  private uint _imageIndex = 0;

  private VkFence[] _waitFences = [];

  public delegate void RenderDelegate();

  public VkFormat DepthFormat { get; private set; }
  public VkDescriptorSet[] ImageDescriptors { get; private set; } = [];
  private DescriptorSetLayout _postProcessLayout = null!;
  public VkSampler DepthSampler { get; private set; }
  public VkSampler ImageSampler { get; private set; }

  internal class DepthStencil {
    internal VkImage Image;
    internal VkImageView ImageView;
    internal VkDeviceMemory ImageMemory;
    // internal VkDescriptorSet ImageDescriptor = VkDescriptorSet.Null;
  }
  private DepthStencil _depthStencil = new();

  internal class Semaphores {
    internal VkSemaphore PresentComplete;
    internal VkSemaphore RenderComplete;
  }
  private Semaphores _semaphores = new();

  public DynamicRenderer(Window window, VulkanDevice device) {
    _window = window;
    _device = device;

    CommandList = new VulkanCommandList();

    RecreateSwapchain();

    InitVulkan();
    CreateDepthStencil();
    CreateSamplers();
    CreateDescriptors();
  }

  public void UpdateDescriptors() {
    VkDescriptorImageInfo* descriptorImageInfo = stackalloc VkDescriptorImageInfo[2];
    descriptorImageInfo[0] = new() {
      imageLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
      imageView = Swapchain.ImageViews[_imageIndex],
      sampler = ImageSampler // Sampler for the color image
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_DEPTH_READ_ONLY_OPTIMAL,
      imageView = _depthStencil.ImageView,
      sampler = DepthSampler // Sampler for the depth image
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = ImageDescriptors[_imageIndex],
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo[0]
    };
    writeDescriptorSets[1] = new() {
      dstSet = ImageDescriptors[_imageIndex],
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &descriptorImageInfo[1]
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
  }

  private void CreateSamplers() {
    CreateSampler(out var imgSampler);
    CreateSampler(out var depthSampler);

    ImageSampler = imgSampler;
    DepthSampler = depthSampler;
  }

  private void CreateSampler(out VkSampler sampler) {
    VkPhysicalDeviceProperties properties = new();
    vkGetPhysicalDeviceProperties(_device.PhysicalDevice, &properties);

    VkSamplerCreateInfo samplerInfo = new();
    samplerInfo.magFilter = VkFilter.Nearest;
    samplerInfo.minFilter = VkFilter.Nearest;
    samplerInfo.addressModeU = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeV = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeW = VkSamplerAddressMode.Repeat;
    samplerInfo.anisotropyEnable = true;
    samplerInfo.maxAnisotropy = properties.limits.maxSamplerAnisotropy;
    samplerInfo.borderColor = VkBorderColor.IntOpaqueBlack;
    samplerInfo.unnormalizedCoordinates = false;
    samplerInfo.compareEnable = false;
    samplerInfo.compareOp = VkCompareOp.Always;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Nearest;

    vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out sampler).CheckResult();
  }

  private unsafe void CreateDescriptors() {
    _postProcessLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.AllGraphics)
      .AddBinding(1, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.AllGraphics)
      .Build();

    _descriptorPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(100)
      .AddPoolSize(VkDescriptorType.InputAttachment, 10)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 20)
      .Build();

    ImageDescriptors = new VkDescriptorSet[Swapchain.ImageViews.Length];
    for (int i = 0; i < ImageDescriptors.Length; i++) {
      CreateImageDescriptor(i);
    }
  }

  private void CreateImageDescriptor(int index) {
    var setLayout = _postProcessLayout.GetDescriptorSetLayout();

    VkDescriptorSet descriptorSet = new();
    var allocInfo = new VkDescriptorSetAllocateInfo {
      descriptorPool = _descriptorPool.GetVkDescriptorPool(),
      descriptorSetCount = 1,
      pSetLayouts = &setLayout
    };
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkDescriptorImageInfo* descriptorImageInfo = stackalloc VkDescriptorImageInfo[2];
    descriptorImageInfo[0] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = Swapchain.ImageViews[index],
      sampler = ImageSampler
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _depthStencil.ImageView,
      sampler = DepthSampler
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo[0]
    };
    writeDescriptorSets[1] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &descriptorImageInfo[1]
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
    ImageDescriptors[index] = descriptorSet;
  }
  private void PrepareFrame() {
    fixed (VkFence* fences = _waitFences) {
      vkWaitForFences(_device.LogicalDevice, (uint)Swapchain.Images.Length, fences, true, UInt64.MaxValue);
    }

    var result = Swapchain.AcquireNextImage(_semaphores.PresentComplete, out _imageIndex);
    if (result == VkResult.ErrorOutOfDateKHR) {
      RecreateSwapchain();
    }

    if (result != VkResult.Success && result != VkResult.SuboptimalKHR) {
      result.CheckResult();
    }
  }

  private void SubmitFrame() {
    var result = Swapchain.QueuePresent(_device.GraphicsQueue, _imageIndex, _semaphores.RenderComplete);
    if (result != VkResult.Success || result != VkResult.SuboptimalKHR) {
      if (result == VK_ERROR_OUT_OF_DATE_KHR) {
        _window.ResetWindowResizedFlag();
        RecreateSwapchain();
        return;
      } else {
        result.CheckResult();
      }
    }
    FrameIndex = (FrameIndex + 1) % Swapchain.Images.Length;
    vkQueueWaitIdle(_device.GraphicsQueue).CheckResult();
  }

  private unsafe void InitVulkan() {
    VkFenceCreateInfo fenceCreateInfo = new() {
      flags = VK_FENCE_CREATE_SIGNALED_BIT
    };
    _waitFences = new VkFence[Swapchain.Images.Length];
    for (int i = 0; i < _waitFences.Length; i++) {
      vkCreateFence(_device.LogicalDevice, &fenceCreateInfo, null, out _waitFences[i]);
    }

    VkSemaphoreCreateInfo semaphoreCreateInfo = new();
    vkCreateSemaphore(_device.LogicalDevice, &semaphoreCreateInfo, null, out _semaphores.PresentComplete).CheckResult();
    vkCreateSemaphore(_device.LogicalDevice, &semaphoreCreateInfo, null, out _semaphores.RenderComplete).CheckResult();

    // VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[1];
    // waitStages[0] = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

  }

  private void CreateDepthStencil() {
    DepthFormat = FindDepthFormat();

    VkImageCreateInfo imageCI = new();
    imageCI.imageType = VK_IMAGE_TYPE_2D;
    imageCI.format = DepthFormat;
    imageCI.extent = new(Swapchain.Extent2D.width, Swapchain.Extent2D.height, 1);
    imageCI.mipLevels = 1;
    imageCI.arrayLayers = 1;
    imageCI.samples = VK_SAMPLE_COUNT_1_BIT;
    imageCI.tiling = VK_IMAGE_TILING_OPTIMAL;
    imageCI.usage = VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT;

    vkCreateImage(_device.LogicalDevice, &imageCI, null, out _depthStencil.Image);

    VkMemoryRequirements memReqs = new();
    vkGetImageMemoryRequirements(_device.LogicalDevice, _depthStencil.Image, &memReqs);

    VkMemoryAllocateInfo memAllloc = new();
    memAllloc.allocationSize = memReqs.size;
    memAllloc.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, MemoryProperty.DeviceLocal);
    vkAllocateMemory(_device.LogicalDevice, &memAllloc, null, out _depthStencil.ImageMemory).CheckResult();
    vkBindImageMemory(_device.LogicalDevice, _depthStencil.Image, _depthStencil.ImageMemory, 0).CheckResult();

    VkImageViewCreateInfo imageViewCI = new();
    imageViewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
    imageViewCI.image = _depthStencil.Image;
    imageViewCI.format = DepthFormat;
    imageViewCI.subresourceRange.baseMipLevel = 0;
    imageViewCI.subresourceRange.levelCount = 1;
    imageViewCI.subresourceRange.baseArrayLayer = 0;
    imageViewCI.subresourceRange.layerCount = 1;
    imageViewCI.subresourceRange.aspectMask = VK_IMAGE_ASPECT_DEPTH_BIT;
    // Stencil aspect should only be set on depth + stencil formats (VK_FORMAT_D16_UNORM_S8_UINT..VK_FORMAT_D32_SFLOAT_S8_UINT
    if (DepthFormat >= VK_FORMAT_D16_UNORM_S8_UINT) {
      // imageViewCI.subresourceRange.aspectMask |= VK_IMAGE_ASPECT_STENCIL_BIT;
    }
    vkCreateImageView(_device.LogicalDevice, &imageViewCI, null, out _depthStencil.ImageView).CheckResult();
  }

  private VkFormat FindDepthFormat() {
    var items = new List<VkFormat> {
      VkFormat.D32Sfloat,
      VkFormat.D32SfloatS8Uint,
      VkFormat.D24UnormS8Uint
    };
    return _device.FindSupportedFormat(items, VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
  }

  public void RecreateSwapchain() {
    var extent = _window.Extent.ToVkExtent2D();
    while (extent.width == 0 || extent.height == 0 || _window.IsMinimalized) {
      extent = _window.Extent.ToVkExtent2D();
    }

    _device.WaitDevice();

    Swapchain?.Dispose();
    Swapchain = new(_device, extent);

    Logger.Info("Recreated Swapchain");
  }
  public VkRenderPass GetSwapchainRenderPass() {
    return VkRenderPass.Null;
  }

  public VkRenderPass GetPostProcessingPass() {
    return VkRenderPass.Null;
  }
  public VkCommandBuffer BeginFrame(VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    PrepareFrame();

    var commandBuffer = _commandBuffers[_imageIndex];
    vkResetCommandBuffer(commandBuffer, VkCommandBufferResetFlags.None);
    VkCommandBufferBeginInfo beginInfo = new();
    if (level == VkCommandBufferLevel.Secondary) {
      beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUse;
    }
    vkBeginCommandBuffer(commandBuffer, &beginInfo);

    return commandBuffer;
  }

  public void EndFrame() {
    var commandBuffer = _commandBuffers[_imageIndex];
    vkEndCommandBuffer(commandBuffer).CheckResult();

    fixed (VkSemaphore* renderPtr = &_semaphores.RenderComplete)
    fixed (VkSemaphore* presentPtr = &_semaphores.PresentComplete) {
      VkSubmitInfo submitInfo = new();

      VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[1];
      waitStages[0] = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

      submitInfo.waitSemaphoreCount = 1;
      submitInfo.pWaitSemaphores = presentPtr;
      submitInfo.pWaitDstStageMask = waitStages;

      submitInfo.commandBufferCount = 1;
      submitInfo.pCommandBuffers = &commandBuffer;

      submitInfo.signalSemaphoreCount = 1;
      submitInfo.pSignalSemaphores = renderPtr;
      submitInfo.pNext = null;

      // fixed (VkFence* waitFences = _waitFences) {
      //   vkWaitForFences(_device.LogicalDevice, (uint)_waitFences.Length, waitFences, true, UInt64.MaxValue);
      //
      // }

      vkResetFences(_device.LogicalDevice, _waitFences[Swapchain.CurrentFrame]);

      var queueResult = vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _waitFences[Swapchain.CurrentFrame]);
      SubmitFrame();
    }
  }

  public void BeginRendering(VkCommandBuffer commandBuffer) {
    // VkCommandBufferBeginInfo cmdBufInfo = new();
    VkUtils.InsertMemoryBarrier(
        commandBuffer,
        Swapchain.Images[_imageIndex],
        0,
        VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
        VK_IMAGE_LAYOUT_UNDEFINED,
        VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
        VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
        VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
        new VkImageSubresourceRange(VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1)
      );

    VkUtils.InsertMemoryBarrier(
      commandBuffer,
      _depthStencil.Image,
      0,
      VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
      VK_IMAGE_LAYOUT_UNDEFINED,
      VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
      VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
      VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
      new VkImageSubresourceRange(VK_IMAGE_ASPECT_DEPTH_BIT, 0, 1, 0, 1)
    );

    VkRenderingAttachmentInfo colorAttachment = new();
    colorAttachment.imageView = Swapchain.ImageViews[_imageIndex];
    colorAttachment.imageLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL;
    colorAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    colorAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    colorAttachment.clearValue.color = new(0.35f, 0.35f, 0.35f, 0.0f);

    VkRenderingAttachmentInfo depthStencilAttachment = new();
    depthStencilAttachment.imageView = _depthStencil.ImageView;
    depthStencilAttachment.imageLayout = VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL;
    depthStencilAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    depthStencilAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    depthStencilAttachment.clearValue.depthStencil = new(1.0f, 0);

    VkRenderingInfo renderingInfo = new();
    renderingInfo.renderArea = new(0, 0, Swapchain.Extent2D.width, Swapchain.Extent2D.height);
    renderingInfo.layerCount = 1;
    renderingInfo.colorAttachmentCount = 1;
    renderingInfo.pColorAttachments = &colorAttachment;
    renderingInfo.pDepthAttachment = &depthStencilAttachment;
    // renderingInfo.pStencilAttachment = &depthStencilAttachment;

    vkCmdBeginRendering(commandBuffer, &renderingInfo);

    VkViewport viewport = new() {
      x = 0.0f,
      y = 0.0f,
      width = Swapchain.Extent2D.width,
      height = Swapchain.Extent2D.height,
      minDepth = 0.0f,
      maxDepth = 1.0f
    };

    VkRect2D scissor = new(0, 0, Swapchain.Extent2D.width, Swapchain.Extent2D.height);
    vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
    vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
  }

  public void EndRendering(VkCommandBuffer commandBuffer) {
    vkCmdEndRendering(commandBuffer);

    VkUtils.InsertMemoryBarrier(
      commandBuffer,
      Swapchain.Images[_imageIndex],
      VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
      0,
      VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
      VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
      VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
      VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
      new VkImageSubresourceRange(VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1)
    );
  }

  public void CreateCommandBuffers(VkCommandPool commandPool, VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    _commandBuffers = new VkCommandBuffer[Swapchain.Images.Length];

    VkCommandBufferAllocateInfo cmdBufAllocateInfo = new() {
      commandPool = commandPool,
      level = level,
      commandBufferCount = (uint)_commandBuffers.Length
    };

    fixed (VkCommandBuffer* cmdBuffersPtr = _commandBuffers) {
      vkAllocateCommandBuffers(_device.LogicalDevice, &cmdBufAllocateInfo, cmdBuffersPtr).CheckResult();
    }
  }

  public unsafe void Dispose() {
    _descriptorPool?.Dispose();
    _postProcessLayout?.Dispose();

    vkDestroyImageView(_device.LogicalDevice, _depthStencil.ImageView, null);
    vkDestroyImage(_device.LogicalDevice, _depthStencil.Image, null);
    vkFreeMemory(_device.LogicalDevice, _depthStencil.ImageMemory, null);

    vkDestroySemaphore(_device.LogicalDevice, _semaphores.PresentComplete, null);
    vkDestroySemaphore(_device.LogicalDevice, _semaphores.RenderComplete, null);

    foreach (var fence in _waitFences) {
      vkDestroyFence(_device.LogicalDevice, fence, null);
    }

    vkDestroySampler(_device.LogicalDevice, ImageSampler);
    vkDestroySampler(_device.LogicalDevice, DepthSampler);

    Swapchain?.Dispose();
  }


  VulkanSwapchain IRenderer.Swapchain => throw new NotImplementedException();

  public VulkanDynamicSwapchain DynamicSwapchain => Swapchain;
  public VkDescriptorSet PostProcessDecriptor => ImageDescriptors[ImageIndex];
  public VkDescriptorSet PreviousPostProcessDescriptor => ImageDescriptors[Swapchain.PreviousFrame];
  public VkCommandBuffer CurrentCommandBuffer => _commandBuffers[Swapchain.CurrentFrame];
  public int FrameIndex { get; private set; }
  public int ImageIndex => (int)_imageIndex;
  public float AspectRatio => Swapchain.ExtentAspectRatio();
  public DwarfExtent2D Extent2D => Swapchain.Extent2D.FromVkExtent2D();
  public int MAX_FRAMES_IN_FLIGHT => Swapchain.Images.Length;
  public VulkanDynamicSwapchain Swapchain { get; private set; } = null!;
  public CommandList CommandList { get; } = null!;

}