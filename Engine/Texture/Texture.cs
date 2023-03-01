using System.Net.Mime;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using StbImageSharp;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Texture : IDisposable {
  public readonly string TextureName;
  private readonly Device _device = null!;
  private VkImage _textureImage = VkImage.Null;
  private VkDeviceMemory _textureImageMemory = VkDeviceMemory.Null;
  // private Vulkan.Buffer _textureBuffer = null!;
  private VkImageView _imageView = VkImageView.Null;
  private VkSampler _imageSampler = VkSampler.Null;

  public Texture(Device device, string texturePath) {
    TextureName = texturePath;
    _device = device;
    CreateTexture(texturePath);
  }

  private unsafe void CreateTexture(string texturePath, int flip = 1) {
    StbImage.stbi_set_flip_vertically_on_load(flip);

    using var stream = File.OpenRead($"{texturePath}");
    var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
    stream.Dispose();

    var size = img.Width * img.Height * 4;
    var stagingBuffer = new Vulkan.Buffer(
      _device,
      (ulong)(size),
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(Utils.ToIntPtr(img.Data), (ulong)size);
    stagingBuffer.Unmap();

    /*
    _textureBuffer = new Vulkan.Buffer(
      _device,
      (ulong)size,
      VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );
    */

    // _device.CopyBuffer(buffer.GetBuffer(), _textureBuffer.GetBuffer(), (ulong)size);
    // buffer.Dispose();

    CreateImage(
      (uint)img.Width,
      (uint)img.Height,
      VkFormat.R8G8B8A8Srgb,
      VkImageTiling.Optimal,
      VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
      VkMemoryPropertyFlags.DeviceLocal
    );

    CreateImageTransitions(VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal);
    CopyBufferToImage(stagingBuffer.GetBuffer(), _textureImage, img.Width, img.Height);
    CreateImageTransitions(VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal);

    //transitionImageLayout(textureImage, VK_FORMAT_R8G8B8A8_SRGB, VK_IMAGE_LAYOUT_UNDEFINED, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL);
    //copyBufferToImage(stagingBuffer, textureImage, static_cast<uint32_t>(texWidth), static_cast<uint32_t>(texHeight));
    //transitionImageLayout(textureImage, VK_FORMAT_R8G8B8A8_SRGB, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

    stagingBuffer.Dispose();

    //vkDestroyBuffer(device, stagingBuffer, nullptr);
    //vkFreeMemory(device, stagingBufferMemory, nullptr);
    CreateTextureImageView();
    CreateSampler();

    // _textureBuffer.Dispose();
  }

  private unsafe void CreateImage(
    uint width,
    uint height,
    VkFormat format,
    VkImageTiling tiling,
    VkImageUsageFlags imageUsageFlags,
    VkMemoryPropertyFlags memoryPropertyFlags
  ) {
    VkImageCreateInfo imageInfo = new();
    imageInfo.sType = VkStructureType.ImageCreateInfo;
    imageInfo.imageType = VkImageType.Image2D;
    imageInfo.extent.width = width;
    imageInfo.extent.height = height;
    imageInfo.extent.depth = 1;
    imageInfo.mipLevels = 1;
    imageInfo.arrayLayers = 1;

    imageInfo.format = format;
    imageInfo.tiling = tiling;
    imageInfo.initialLayout = VkImageLayout.Undefined;
    imageInfo.usage = imageUsageFlags;
    imageInfo.sharingMode = VkSharingMode.Exclusive;
    imageInfo.samples = VkSampleCountFlags.Count1;
    imageInfo.flags = 0;

    vkCreateImage(_device.LogicalDevice, &imageInfo, null, out _textureImage).CheckResult();

    VkMemoryRequirements memRequirements;
    vkGetImageMemoryRequirements(_device.LogicalDevice, _textureImage, out memRequirements);

    VkMemoryAllocateInfo allocInfo = new();
    allocInfo.sType = VkStructureType.MemoryAllocateInfo;
    allocInfo.allocationSize = memRequirements.size;
    allocInfo.memoryTypeIndex = _device.FindMemoryType(memRequirements.memoryTypeBits, memoryPropertyFlags);

    vkAllocateMemory(_device.LogicalDevice, &allocInfo, null, out _textureImageMemory).CheckResult();
    vkBindImageMemory(_device.LogicalDevice, _textureImage, _textureImageMemory, 0).CheckResult();
  }

  private unsafe void CopyBufferToImage(VkBuffer buffer, VkImage image, int width, int height) {
    VkCommandBuffer commandBuffer = _device.BeginSingleTimeCommands();

    VkBufferImageCopy region = new();
    region.bufferOffset = 0;
    region.bufferRowLength = 0;
    region.bufferImageHeight = 0;
    region.imageSubresource.aspectMask = VkImageAspectFlags.Color;
    region.imageSubresource.mipLevel = 0;
    region.imageSubresource.baseArrayLayer = 0;
    region.imageSubresource.layerCount = 1;
    region.imageOffset = new(0, 0, 0);
    region.imageExtent = new(width, height, 1);

    vkCmdCopyBufferToImage(commandBuffer, buffer, image, VkImageLayout.TransferDstOptimal, 1, &region);

    _device.EndSingleTimeCommands(commandBuffer);
  }

  private unsafe void CreateImageTransitions(VkImageLayout oldLayout, VkImageLayout newLayout) {
    VkCommandBuffer commandBuffer = _device.BeginSingleTimeCommands();

    VkImageMemoryBarrier barrier = new();
    barrier.sType = VkStructureType.ImageMemoryBarrier;
    barrier.oldLayout = oldLayout;
    barrier.newLayout = newLayout;
    barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    barrier.image = _textureImage;
    barrier.subresourceRange.aspectMask = VkImageAspectFlags.Color;
    barrier.subresourceRange.baseMipLevel = 0;
    barrier.subresourceRange.levelCount = 1;
    barrier.subresourceRange.baseArrayLayer = 0;
    barrier.subresourceRange.layerCount = 1;

    VkPipelineStageFlags sourceStage = new();
    VkPipelineStageFlags destinationStage = new();

    if (oldLayout == VkImageLayout.Undefined && newLayout == VkImageLayout.TransferDstOptimal) {
      barrier.srcAccessMask = 0;
      barrier.dstAccessMask = VkAccessFlags.TransferWrite;

      sourceStage = VkPipelineStageFlags.TopOfPipe;
      destinationStage = VkPipelineStageFlags.Transfer;
    } else if (oldLayout == VkImageLayout.TransferDstOptimal && newLayout == VkImageLayout.ShaderReadOnlyOptimal) {
      barrier.srcAccessMask = VkAccessFlags.TransferWrite;
      barrier.dstAccessMask = VkAccessFlags.ShaderRead;

      sourceStage = VkPipelineStageFlags.Transfer;
      destinationStage = VkPipelineStageFlags.FragmentShader;
    } else {
      Logger.Error($"Unsupported layout transition");
    }

    vkCmdPipelineBarrier(
      commandBuffer,
      sourceStage,
      destinationStage,
      0,
      0, null,
      0, null,
      1, &barrier
    );

    _device.EndSingleTimeCommands(commandBuffer);
  }

  private void CreateTextureImageView() {
    _imageView = CreateImageView(VkFormat.R8G8B8A8Srgb);
  }

  private unsafe VkImageView CreateImageView(VkFormat format) {
    VkImageViewCreateInfo viewInfo = new();
    viewInfo.sType = VkStructureType.ImageViewCreateInfo;
    viewInfo.image = _textureImage;
    viewInfo.viewType = VkImageViewType.Image2D;
    viewInfo.format = format;
    viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
    viewInfo.subresourceRange.baseMipLevel = 0;
    viewInfo.subresourceRange.levelCount = 1;
    viewInfo.subresourceRange.baseArrayLayer = 0;
    viewInfo.subresourceRange.layerCount = 1;

    VkImageView view;
    vkCreateImageView(_device.LogicalDevice, &viewInfo, null, out view).CheckResult();
    return view;
  }

  private unsafe void CreateSampler() {
    VkPhysicalDeviceProperties properties = new();
    vkGetPhysicalDeviceProperties(_device.PhysicalDevice, &properties);

    VkSamplerCreateInfo samplerInfo = new();
    samplerInfo.sType = VkStructureType.SamplerCreateInfo;
    samplerInfo.magFilter = VkFilter.Linear;
    samplerInfo.minFilter = VkFilter.Linear;
    samplerInfo.addressModeU = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeV = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeW = VkSamplerAddressMode.Repeat;
    samplerInfo.anisotropyEnable = true;
    samplerInfo.maxAnisotropy = properties.limits.maxSamplerAnisotropy;
    samplerInfo.borderColor = VkBorderColor.IntOpaqueBlack;
    samplerInfo.unnormalizedCoordinates = false;
    samplerInfo.compareEnable = false;
    samplerInfo.compareOp = VkCompareOp.Always;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Linear;

    vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out _imageSampler).CheckResult();
  }

  public unsafe void Dispose() {
    vkFreeMemory(_device.LogicalDevice, _textureImageMemory);
    vkDestroyImage(_device.LogicalDevice, _textureImage);
    vkDestroyImageView(_device.LogicalDevice, _imageView);
    vkDestroySampler(_device.LogicalDevice, _imageSampler);
    // _textureBuffer.Dispose();
  }

  // public Vulkan.Buffer GetBuffer() => _textureBuffer;
  public VkSampler GetSampler() => _imageSampler;
  public VkImageView GetImageView() => _imageView;
}