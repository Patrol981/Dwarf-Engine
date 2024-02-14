using System;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using OpenTK.Compute.OpenCL;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public ref struct SwapChainSupportDetails {
  public VkSurfaceCapabilitiesKHR Capabilities;
  public ReadOnlySpan<VkSurfaceFormatKHR> Formats;
  public ReadOnlySpan<VkPresentModeKHR> PresentModes;
}
public static class VkUtils {
  public static unsafe void MemCopy(nint destination, nint source, int byteCount) {
    if (byteCount <= 0) {
      throw new Exception("ByteCount is NULL");
    }

    if (byteCount > 2130702268) {
      throw new Exception("ByteCount is too big");
    }

    System.Buffer.MemoryCopy((void*)source, (void*)destination, byteCount, byteCount);
  }

  public static void MemCopy(ref byte src, ref byte dst, uint byteCount) {
    Unsafe.CopyBlock(ref dst, ref src, byteCount);
  }

  public static SwapChainSupportDetails QuerySwapChainSupport(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface) {
    SwapChainSupportDetails details = new SwapChainSupportDetails();
    vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, out details.Capabilities).CheckResult();

    details.Formats = vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface);
    details.PresentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface);
    return details;
  }

  public static IntPtr ToIntPtr<T>(T[] arr) where T : struct {
    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;
    try {
      ptr = Marshal.AllocHGlobal(size * arr.Length);
      for (int i = 0; i < arr.Length; i++) {
        Marshal.StructureToPtr(arr[i], IntPtr.Add(ptr, i * size), true);
      }
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }

  public static IntPtr ToIntPtr<T>(T data) where T : struct {
    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;
    try {
      ptr = Marshal.AllocHGlobal(size);
      Marshal.StructureToPtr(data, ptr, true);
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }

  public static void SetImageLayout(
    VkCommandBuffer commandBuffer,
    VkImage image,
    VkImageAspectFlags aspectMask,
    VkImageLayout oldImageLayout,
    VkImageLayout newImageLayout,
    VkPipelineStageFlags srcStageFlags = VkPipelineStageFlags.AllCommands,
    VkPipelineStageFlags dstStageFlags = VkPipelineStageFlags.AllCommands
  ) {
    VkImageSubresourceRange subresourceRange = new();
    subresourceRange.aspectMask = aspectMask;
    subresourceRange.baseMipLevel = 0;
    subresourceRange.levelCount = 1;
    subresourceRange.layerCount = 1;
    SetImageLayout(commandBuffer, image, oldImageLayout, newImageLayout, subresourceRange, srcStageFlags, dstStageFlags);
  }

  public static void SetImageLayout(
    VkCommandBuffer commandBuffer,
    VkImage image,
    VkImageLayout oldImageLayout,
    VkImageLayout newImageLayout,
    VkImageSubresourceRange subresourceRange,
    VkPipelineStageFlags srcStageFlags = VkPipelineStageFlags.AllCommands,
    VkPipelineStageFlags dstStageFlags = VkPipelineStageFlags.AllCommands
  ) {
    var imageMemoryBarrier = new VkImageMemoryBarrier();
    imageMemoryBarrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    imageMemoryBarrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    imageMemoryBarrier.oldLayout = oldImageLayout;
    imageMemoryBarrier.newLayout = newImageLayout;
    imageMemoryBarrier.image = image;
    imageMemoryBarrier.subresourceRange = subresourceRange;

    _ = oldImageLayout switch {
      VkImageLayout.Undefined => imageMemoryBarrier.srcAccessMask = 0,
      VkImageLayout.Preinitialized => imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite,
      VkImageLayout.ColorAttachmentOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.ColorAttachmentWrite,
      VkImageLayout.DepthStencilAttachmentOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite,
      VkImageLayout.TransferSrcOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead,
      VkImageLayout.TransferDstOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferWrite,
      VkImageLayout.ShaderReadOnlyOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.ShaderRead,
      _ => imageMemoryBarrier.dstAccessMask = VkAccessFlags.None
    };

    _ = newImageLayout switch {
      VkImageLayout.TransferDstOptimal => imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferWrite,
      VkImageLayout.TransferSrcOptimal => imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferRead,
      VkImageLayout.ColorAttachmentOptimal => imageMemoryBarrier.dstAccessMask = VkAccessFlags.ColorAttachmentWrite,
      VkImageLayout.DepthStencilAttachmentOptimal => imageMemoryBarrier.dstAccessMask |= VkAccessFlags.DepthStencilAttachmentWrite,
      _ => imageMemoryBarrier.dstAccessMask = VkAccessFlags.None
    };

    if (newImageLayout == VkImageLayout.ShaderReadOnlyOptimal) {
      if (imageMemoryBarrier.srcAccessMask == 0) {
        imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite | VkAccessFlags.TransferWrite;
      }
      imageMemoryBarrier.dstAccessMask = VkAccessFlags.ShaderRead;
    }

    unsafe {
      vkCmdPipelineBarrier(
        commandBuffer,
        srcStageFlags,
        dstStageFlags,
        0,
        0, null,
        0, null,
        1, &imageMemoryBarrier
     );
    }
  }

  public static VkViewport Viewport(float width, float height, float minDepth, float maxDepth) {
    VkViewport viewport = new();
    viewport.width = width;
    viewport.height = height;
    viewport.minDepth = minDepth;
    viewport.maxDepth = maxDepth;
    return viewport;
  }

  public static VkDescriptorPoolSize DescriptorPoolSize(VkDescriptorType type, uint count) {
    VkDescriptorPoolSize descriptorPoolSize = new();
    descriptorPoolSize.type = type;
    descriptorPoolSize.descriptorCount = count;
    return descriptorPoolSize;
  }

  public unsafe static VkDescriptorPoolCreateInfo DescriptorPoolCreateInfo(
    VkDescriptorPoolSize[] poolSizes,
    uint maxSets
  ) {

    VkDescriptorPoolCreateInfo descriptorPoolInfo = new();
    descriptorPoolInfo.poolSizeCount = (uint)poolSizes.Length;
    fixed (VkDescriptorPoolSize* poolSizesPtr = poolSizes) {
      descriptorPoolInfo.pPoolSizes = poolSizesPtr;
    }
    descriptorPoolInfo.maxSets = maxSets;
    return descriptorPoolInfo;
  }

  public unsafe static VkDescriptorSetLayoutBinding DescriptorSetLayoutBinding(
    VkDescriptorType type,
    VkShaderStageFlags stageFlags,
    uint binding,
    uint descriptorCount = 1
  ) {
    VkDescriptorSetLayoutBinding setLayoutBinding = new();
    setLayoutBinding.descriptorType = type;
    setLayoutBinding.stageFlags = stageFlags;
    setLayoutBinding.binding = binding;
    setLayoutBinding.descriptorCount = descriptorCount;
    return setLayoutBinding;
  }

  public unsafe static VkDescriptorSetLayoutCreateInfo DescriptorSetLayoutCreateInfo(
    VkDescriptorSetLayoutBinding[] bindings
  ) {
    VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new();
    fixed (VkDescriptorSetLayoutBinding* bindingsPtr = bindings) {
      descriptorSetLayoutCreateInfo.pBindings = bindingsPtr;
    }
    descriptorSetLayoutCreateInfo.bindingCount = (uint)bindings.Length;
    return descriptorSetLayoutCreateInfo;
  }

  public unsafe static VkDescriptorSetAllocateInfo DescriptorSetAllocateInfo(
    VkDescriptorPool descriptorPool,
    VkDescriptorSetLayout* pSetLayouts,
    uint descriptorSetCount
  ) {
    VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new();
    descriptorSetAllocateInfo.descriptorPool = descriptorPool;
    descriptorSetAllocateInfo.pSetLayouts = pSetLayouts;
    descriptorSetAllocateInfo.descriptorSetCount = descriptorSetCount;
    return descriptorSetAllocateInfo;
  }

  public unsafe static VkDescriptorImageInfo DescriptorImageInfo(
    VkSampler sampler,
    VkImageView imageView,
    VkImageLayout imageLayout
  ) {
    VkDescriptorImageInfo descriptorImageInfo = new();
    descriptorImageInfo.sampler = sampler;
    descriptorImageInfo.imageView = imageView;
    descriptorImageInfo.imageLayout = imageLayout;
    return descriptorImageInfo;
  }

  public unsafe static VkWriteDescriptorSet WriteDescriptorSet(
    VkDescriptorSet dstSet,
    VkDescriptorType type,
    uint binding,
    VkDescriptorImageInfo imageInfo,
    uint descriptorCount = 1
  ) {
    VkWriteDescriptorSet writeDescriptorSet = new();
    writeDescriptorSet.dstSet = dstSet;
    writeDescriptorSet.descriptorType = type;
    writeDescriptorSet.dstBinding = binding;
    writeDescriptorSet.pImageInfo = &imageInfo;
    writeDescriptorSet.descriptorCount = descriptorCount;
    return writeDescriptorSet;
  }

  public static VkPushConstantRange PushConstantRange(VkShaderStageFlags stageFlags, uint size, uint offset) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = stageFlags;
    pushConstantRange.offset = offset;
    pushConstantRange.size = size;
    return pushConstantRange;
  }

  public unsafe static VkPipelineLayoutCreateInfo PipelineLayoutCreateInfo(
    VkDescriptorSetLayout* setLayouts,
    uint count
  ) {
    VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new();
    pipelineLayoutCreateInfo.setLayoutCount = count;
    pipelineLayoutCreateInfo.pSetLayouts = setLayouts;
    return pipelineLayoutCreateInfo;
  }

  public static VkPipelineInputAssemblyStateCreateInfo PipelineInputAssemblyStateCreateInfo(
    VkPrimitiveTopology topology,
    VkPipelineInputAssemblyStateCreateFlags flags,
    bool primitiveRestartEnable
  ) {
    VkPipelineInputAssemblyStateCreateInfo pipelineInputAssemblyStateCreateInfo = new();
    pipelineInputAssemblyStateCreateInfo.topology = topology;
    pipelineInputAssemblyStateCreateInfo.flags = flags;
    pipelineInputAssemblyStateCreateInfo.primitiveRestartEnable = primitiveRestartEnable;
    return pipelineInputAssemblyStateCreateInfo;
  }

  public static VkPipelineRasterizationStateCreateInfo PipelineRasterizationStateCreateInfo(
    VkPolygonMode polygonMode,
    VkCullModeFlags cullMode,
    VkFrontFace frontFace,
    VkPipelineRasterizationStateCreateFlags flags = 0
  ) {
    VkPipelineRasterizationStateCreateInfo pipelineRasterizationStateCreateInfo = new();
    pipelineRasterizationStateCreateInfo.polygonMode = polygonMode;
    pipelineRasterizationStateCreateInfo.cullMode = cullMode;
    pipelineRasterizationStateCreateInfo.frontFace = frontFace;
    pipelineRasterizationStateCreateInfo.flags = flags;
    pipelineRasterizationStateCreateInfo.depthClampEnable = false;
    pipelineRasterizationStateCreateInfo.lineWidth = 1.0f;
    return pipelineRasterizationStateCreateInfo;
  }

  public unsafe static VkPipelineColorBlendStateCreateInfo PipelineColorBlendStateCreateInfo(
    uint count,
    VkPipelineColorBlendAttachmentState* pAttachments
  ) {
    VkPipelineColorBlendStateCreateInfo pipelineColorBlendStateCreateInfo = new();
    pipelineColorBlendStateCreateInfo.attachmentCount = count;
    pipelineColorBlendStateCreateInfo.pAttachments = pAttachments;
    return pipelineColorBlendStateCreateInfo;
  }

  public unsafe static VkPipelineDepthStencilStateCreateInfo PipelineDepthStencilStateCreateInfo(
    bool depthTestEnable,
    bool depthWriteEnable,
    VkCompareOp depthCompareOp
  ) {
    VkPipelineDepthStencilStateCreateInfo pipelineDepthStencilStateCreateInfo = new();
    pipelineDepthStencilStateCreateInfo.depthTestEnable = depthTestEnable;
    pipelineDepthStencilStateCreateInfo.depthWriteEnable = depthWriteEnable;
    pipelineDepthStencilStateCreateInfo.depthCompareOp = depthCompareOp;
    pipelineDepthStencilStateCreateInfo.back.compareOp = VkCompareOp.Always;
    return pipelineDepthStencilStateCreateInfo;
  }

  public unsafe static VkPipelineViewportStateCreateInfo PipelineViewportStateCreateInfo(
    uint viewportCount,
    uint scissorCount,
    VkPipelineViewportStateCreateFlags flags = VkPipelineViewportStateCreateFlags.None
  ) {
    VkPipelineViewportStateCreateInfo pipelineViewportStateCreateInfo = new();
    pipelineViewportStateCreateInfo.viewportCount = viewportCount;
    pipelineViewportStateCreateInfo.scissorCount = scissorCount;
    pipelineViewportStateCreateInfo.flags = flags;
    return pipelineViewportStateCreateInfo;
  }

  public unsafe static VkPipelineMultisampleStateCreateInfo PipelineMultisampleStateCreateInfo(
    VkSampleCountFlags rasterizationSamples,
    VkPipelineMultisampleStateCreateFlags flags = VkPipelineMultisampleStateCreateFlags.None
  ) {
    VkPipelineMultisampleStateCreateInfo pipelineMultisampleStateCreateInfo = new();
    pipelineMultisampleStateCreateInfo.rasterizationSamples = rasterizationSamples;
    pipelineMultisampleStateCreateInfo.flags = flags;
    return pipelineMultisampleStateCreateInfo;
  }

  public unsafe static VkPipelineDynamicStateCreateInfo PipelineDynamicStateCreateInfo(
    VkDynamicState[] dynamicStates,
    VkPipelineDynamicStateCreateFlags flags = VkPipelineDynamicStateCreateFlags.None
  ) {
    VkPipelineDynamicStateCreateInfo pipelineDynamicStateCreateInfo = new();
    fixed (VkDynamicState* pDynamicStates = dynamicStates) {
      pipelineDynamicStateCreateInfo.pDynamicStates = pDynamicStates;
    }
    pipelineDynamicStateCreateInfo.dynamicStateCount = (uint)dynamicStates.Length;
    pipelineDynamicStateCreateInfo.flags = flags;
    return pipelineDynamicStateCreateInfo;
  }

  public unsafe static VkGraphicsPipelineCreateInfo PipelineCreateInfo(
    VkPipelineLayout layout,
    VkRenderPass renderPass,
    VkPipelineCreateFlags flags = VkPipelineCreateFlags.None
  ) {
    VkGraphicsPipelineCreateInfo pipelineCreateInfo = new();
    pipelineCreateInfo.layout = layout;
    pipelineCreateInfo.renderPass = renderPass;
    pipelineCreateInfo.flags = flags;
    pipelineCreateInfo.basePipelineIndex = -1;
    pipelineCreateInfo.basePipelineHandle = VkPipeline.Null;
    return pipelineCreateInfo;
  }

  public unsafe static VkVertexInputBindingDescription VertexInputBindingDescription(
    uint binding,
    uint stride,
    VkVertexInputRate inputRate
  ) {
    VkVertexInputBindingDescription vInputBindDescription = new();
    vInputBindDescription.binding = binding;
    vInputBindDescription.stride = stride;
    vInputBindDescription.inputRate = inputRate;
    return vInputBindDescription;
  }

  public unsafe static VkVertexInputAttributeDescription VertexInputAttributeDescription(
    uint binding,
    uint location,
    VkFormat format,
    uint offset
  ) {
    VkVertexInputAttributeDescription vInputAttribDescription = new();
    vInputAttribDescription.location = location;
    vInputAttribDescription.binding = binding;
    vInputAttribDescription.format = format;
    vInputAttribDescription.offset = offset;
    return vInputAttribDescription;
  }
}