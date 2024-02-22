using System.Drawing;
using System.Net.Mime;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;
using Dwarf.Vulkan;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Texture : IDisposable {
  public readonly string TextureName;
  protected readonly VulkanDevice _device = null!;

  internal VkImage _textureImage = VkImage.Null;
  internal VkDeviceMemory _textureImageMemory = VkDeviceMemory.Null;
  internal VkImageView _imageView = VkImageView.Null;
  internal VkSampler _imageSampler = VkSampler.Null;

  protected int _width = 0;
  protected int _height = 0;
  protected int _size = 0;

  public Texture(VulkanDevice device, int width, int height, string textureName = "") {
    _device = device;
    _width = width;
    _height = height;
    TextureName = textureName;

    _size = _width * _height * 4;
  }

  public void SetTextureData(nint dataPtr, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    var stagingBuffer = new Vulkan.Buffer(
      _device,
      (ulong)_size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    var data = new byte[_size];
    Marshal.Copy(dataPtr, data, 0, data.Length);
    if (MemoryUtils.IsNull(data)) {
      Logger.Warn($"[Texture Bytes] Memory is null");
      return;
    }

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(data), (ulong)_size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer, createFlags);
  }

  public void SetTextureData(byte[] data, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    var stagingBuffer = new Vulkan.Buffer(
      _device,
      (ulong)_size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(data), (ulong)_size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer, createFlags);
  }

  private void ProcessTexture(Vulkan.Buffer stagingBuffer, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    unsafe {
      if (_textureImage.IsNotNull) {
        _device.WaitDevice();
        vkDestroyImage(_device.LogicalDevice, _textureImage);
      }

      if (_textureImageMemory.IsNotNull) {
        _device.WaitDevice();
        vkFreeMemory(_device.LogicalDevice, _textureImageMemory);
      }
    }

    CreateImage(
      _device,
      (uint)_width,
      (uint)_height,
      VkFormat.R8G8B8A8Unorm,
      VkImageTiling.Optimal,
      VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
      MemoryProperty.DeviceLocal,
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

  public static async Task<Texture> LoadFromPath(VulkanDevice device, string path, int flip = 1, VkImageCreateFlags imageCreateFlags = VkImageCreateFlags.None) {
    var textureData = await LoadDataFromPath(path, flip);
    var texture = new Texture(device, textureData.Width, textureData.Height, path);
    texture.SetTextureData(textureData.Data, imageCreateFlags);
    return texture;
  }

  public static async Task<ImageResult> LoadDataFromPath(string path, int flip = 1) {
    StbImage.stbi_set_flip_vertically_on_load(flip);

    using var stream = File.OpenRead(path);
    var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
    await stream.DisposeAsync();
    return img;
  }

  public static ImageResult LoadDataFromBytes(byte[] data, int flip = 1) {
    StbImage.stbi_set_flip_vertically_on_load(flip);

    using var stream = new MemoryStream(data);
    var image = ImageResult.FromStream(stream);
    return image;
  }

  private unsafe static void CreateImage(
    VulkanDevice device,
    uint width,
    uint height,
    VkFormat format,
    VkImageTiling tiling,
    VkImageUsageFlags imageUsageFlags,
    MemoryProperty memoryPropertyFlags,
    out VkImage textureImage,
    out VkDeviceMemory textureImageMemory,
    VkImageCreateFlags createFlags = VkImageCreateFlags.None
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
    imageInfo.flags = createFlags;

    vkCreateImage(device.LogicalDevice, &imageInfo, null, out textureImage).CheckResult();
    vkGetImageMemoryRequirements(device.LogicalDevice, textureImage, out VkMemoryRequirements memRequirements);

    VkMemoryAllocateInfo allocInfo = new();
    allocInfo.allocationSize = memRequirements.size;
    allocInfo.memoryTypeIndex = device.FindMemoryType(memRequirements.memoryTypeBits, (VkMemoryPropertyFlags)memoryPropertyFlags);

    vkAllocateMemory(device.LogicalDevice, &allocInfo, null, out textureImageMemory).CheckResult();
    vkBindImageMemory(device.LogicalDevice, textureImage, textureImageMemory, 0).CheckResult();
  }

  private unsafe static void CopyBufferToImage(VulkanDevice device, VkBuffer buffer, VkImage image, int width, int height) {
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

    vkCmdCopyBufferToImage(commandBuffer, buffer, image, VkImageLayout.TransferDstOptimal, 1, &region);

    device.EndSingleTimeCommands(commandBuffer);
  }

  private unsafe static void CreateImageTransitions(
    VulkanDevice device,
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

    vkCmdPipelineBarrier(
      commandBuffer,
      sourceStage,
      destinationStage,
      0,
      0, null,
      0, null,
      1, &barrier
    );

    device.EndSingleTimeCommands(commandBuffer);
  }

  private static void CreateTextureImageView(VulkanDevice device, VkImage textureImage, out VkImageView imageView) {
    imageView = CreateImageView(device, VkFormat.R8G8B8A8Unorm, textureImage);
  }

  private unsafe static VkImageView CreateImageView(VulkanDevice device, VkFormat format, VkImage textureImage) {
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

  private unsafe static void CreateSampler(VulkanDevice device, out VkSampler imageSampler) {
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

  public unsafe virtual void Dispose(bool disposing) {
    if (disposing) {
      vkFreeMemory(_device.LogicalDevice, _textureImageMemory);
      vkDestroyImage(_device.LogicalDevice, _textureImage);
      vkDestroyImageView(_device.LogicalDevice, _imageView);
      vkDestroySampler(_device.LogicalDevice, _imageSampler);
    }
  }

  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  public VkSampler GetSampler() => _imageSampler;
  public VkImageView GetImageView() => _imageView;
  public VkImage GetTextureImage() => _textureImage;
  public int Width => _width;
  public int Height => _height;
  public int Size => _size;
}