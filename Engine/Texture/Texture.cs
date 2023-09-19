using System.Drawing;
using System.Net.Mime;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using OpenTK.Compute.OpenCL;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Texture : IDisposable {
  public readonly string TextureName;
  private readonly Device _device = null!;

  internal VkImage _textureImage = VkImage.Null;
  internal VkDeviceMemory _textureImageMemory = VkDeviceMemory.Null;
  internal VkImageView _imageView = VkImageView.Null;
  internal VkSampler _imageSampler = VkSampler.Null;

  private int _width = 0;
  private int _height = 0;
  private int _size = 0;

  public Texture(Device device, int width, int height, string textureName = "") {
    _device = device;
    _width = width;
    _height = height;
    TextureName = textureName;

    _size = _width * _height * 4;
  }

  public void SetTextureData(byte[] data) {
    var stagingBuffer = new Vulkan.Buffer(
      _device,
      (ulong)(_size),
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(data), (ulong)_size);
    stagingBuffer.Unmap();

    unsafe {
      if (_textureImage.IsNotNull) {
        vkDeviceWaitIdle(_device.LogicalDevice);
        vkDestroyImage(_device.LogicalDevice, _textureImage);
      }

      if (_textureImageMemory.IsNotNull) {
        vkDeviceWaitIdle(_device.LogicalDevice);
        vkFreeMemory(_device.LogicalDevice, _textureImageMemory);
      }
    }

    CreateImage(
      _device,
      (uint)_width,
      (uint)_height,
      VkFormat.R8G8B8A8Srgb,
      VkImageTiling.Optimal,
      VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
      VkMemoryPropertyFlags.DeviceLocal,
      out _textureImage,
      out _textureImageMemory
    );

    CreateImageTransitions(
      _device,
      VkImageLayout.Undefined,
      VkImageLayout.TransferDstOptimal,
      _textureImage
    );

    CopyBufferToImage(
      _device,
      stagingBuffer.GetBuffer(),
      _textureImage,
      _width,
      _height
    );

    CreateImageTransitions(
      _device,
      VkImageLayout.TransferDstOptimal,
      VkImageLayout.ShaderReadOnlyOptimal,
      _textureImage
    );

    stagingBuffer.Dispose();

    CreateTextureImageView(_device, _textureImage, out _imageView);
    CreateSampler(_device, out _imageSampler);
  }

  public static async Task<ImageResult> LoadFromPath(string path, int flip = 1) {
    StbImage.stbi_set_flip_vertically_on_load(flip);

    using var stream = File.OpenRead(path);
    var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
    await stream.DisposeAsync();
    return img;
  }

  private unsafe static void CreateImage(
    Device device,
    uint width,
    uint height,
    VkFormat format,
    VkImageTiling tiling,
    VkImageUsageFlags imageUsageFlags,
    VkMemoryPropertyFlags memoryPropertyFlags,
    out VkImage textureImage,
    out VkDeviceMemory textureImageMemory
  ) {
    VkImageCreateInfo imageInfo = new();
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

    vkCreateImage(device.LogicalDevice, &imageInfo, null, out textureImage).CheckResult();

    VkMemoryRequirements memRequirements;
    vkGetImageMemoryRequirements(device.LogicalDevice, textureImage, out memRequirements);

    VkMemoryAllocateInfo allocInfo = new();
    // allocInfo.sType = VkStructureType.MemoryAllocateInfo;
    allocInfo.allocationSize = memRequirements.size;
    allocInfo.memoryTypeIndex = device.FindMemoryType(memRequirements.memoryTypeBits, memoryPropertyFlags);

    vkAllocateMemory(device.LogicalDevice, &allocInfo, null, out textureImageMemory).CheckResult();
    vkBindImageMemory(device.LogicalDevice, textureImage, textureImageMemory, 0).CheckResult();
  }

  private unsafe static void CopyBufferToImage(Device device, VkBuffer buffer, VkImage image, int width, int height) {
    VkCommandBuffer commandBuffer = device.BeginSingleTimeCommands();

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

    // device._mutex.WaitOne();
    vkCmdCopyBufferToImage(commandBuffer, buffer, image, VkImageLayout.TransferDstOptimal, 1, &region);
    // device._mutex.ReleaseMutex();

    device.EndSingleTimeCommands(commandBuffer);
  }

  private unsafe static void CreateImageTransitions(
    Device device,
    VkImageLayout oldLayout,
    VkImageLayout newLayout,
    VkImage textureImage
  ) {
    VkCommandBuffer commandBuffer = device.BeginSingleTimeCommands();

    VkImageMemoryBarrier barrier = new();
    barrier.oldLayout = oldLayout;
    barrier.newLayout = newLayout;
    barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    barrier.image = textureImage;
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

    device._mutex.WaitOne();
    vkCmdPipelineBarrier(
      commandBuffer,
      sourceStage,
      destinationStage,
      0,
      0, null,
      0, null,
      1, &barrier
    );
    device._mutex.ReleaseMutex();

    device.EndSingleTimeCommands(commandBuffer);
  }

  private static void CreateTextureImageView(Device device, VkImage textureImage, out VkImageView imageView) {
    imageView = CreateImageView(device, VkFormat.R8G8B8A8Srgb, textureImage);
  }

  private unsafe static VkImageView CreateImageView(Device device, VkFormat format, VkImage textureImage) {
    VkImageViewCreateInfo viewInfo = new();
    viewInfo.image = textureImage;
    viewInfo.viewType = VkImageViewType.Image2D;
    viewInfo.format = format;
    viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
    viewInfo.subresourceRange.baseMipLevel = 0;
    viewInfo.subresourceRange.levelCount = 1;
    viewInfo.subresourceRange.baseArrayLayer = 0;
    viewInfo.subresourceRange.layerCount = 1;

    VkImageView view;
    vkCreateImageView(device.LogicalDevice, &viewInfo, null, out view).CheckResult();
    return view;
  }

  private unsafe static void CreateSampler(Device device, out VkSampler imageSampler) {
    VkPhysicalDeviceProperties properties = new();
    vkGetPhysicalDeviceProperties(device.PhysicalDevice, &properties);

    VkSamplerCreateInfo samplerInfo = new();
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

    vkCreateSampler(device.LogicalDevice, &samplerInfo, null, out imageSampler).CheckResult();
  }

  public unsafe void Dispose() {
    vkFreeMemory(_device.LogicalDevice, _textureImageMemory);
    vkDestroyImage(_device.LogicalDevice, _textureImage);
    vkDestroyImageView(_device.LogicalDevice, _imageView);
    vkDestroySampler(_device.LogicalDevice, _imageSampler);
  }

  public VkSampler GetSampler() => _imageSampler;
  public VkImageView GetImageView() => _imageView;
  public VkImage GetTextureImage() => _textureImage;
  public int Width => _width;
  public int Height => _height;
  public int Size => _size;
}