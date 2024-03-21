using System.Runtime.InteropServices;

using Assimp;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;
using Dwarf.Vulkan;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class VulkanTexture : ITexture {
  protected readonly VulkanDevice _device = null!;

  internal VkImage _textureImage = VkImage.Null;
  internal VkDeviceMemory _textureImageMemory = VkDeviceMemory.Null;
  internal VkImageView _imageView = VkImageView.Null;
  internal VkSampler _imageSampler = VkSampler.Null;

  protected int _width = 0;
  protected int _height = 0;
  protected int _size = 0;

  public VulkanTexture(VulkanDevice device, int width, int height, string textureName = "") {
    _device = device;
    _width = width;
    _height = height;
    TextureName = textureName;

    _size = _width * _height * 4;
  }

  public void SetTextureData(nint dataPtr) {
    SetTextureData(dataPtr, VkImageCreateFlags.None);
  }
  private void SetTextureData(nint dataPtr, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    var stagingBuffer = new DwarfBuffer(
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
    stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(data), (ulong)_size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer, createFlags);
  }

  public void SetTextureData(byte[] data) {
    SetTextureData(data, VkImageCreateFlags.None);
  }

  private void SetTextureData(byte[] data, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    var stagingBuffer = new DwarfBuffer(
      _device,
      (ulong)_size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(data), (ulong)_size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer, createFlags);
  }

  private void ProcessTexture(DwarfBuffer stagingBuffer, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
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

    HandleTexture(stagingBuffer.GetBuffer(), VkFormat.R8G8B8A8Unorm, _width, _height);

    CreateTextureImageView(_device, _textureImage, out _imageView);
    CreateSampler(_device, out _imageSampler);

    stagingBuffer.Dispose();

    /*

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
    */
  }

  public static async Task<ITexture> LoadFromPath(VulkanDevice device, string path, int flip = 1, VkImageCreateFlags imageCreateFlags = VkImageCreateFlags.None) {
    var textureData = await LoadDataFromPath(path, flip);
    var texture = new VulkanTexture(device, textureData.Width, textureData.Height, path);
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

  private unsafe void HandleTexture(VkBuffer stagingBuffer, VkFormat format, int width, int height) {
    var copyCmd = _device.CreateCommandBuffer(VkCommandBufferLevel.Primary, true);

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

    var subresourceRange = new VkImageSubresourceRange();
    subresourceRange.aspectMask = VkImageAspectFlags.Color;
    subresourceRange.baseMipLevel = 0;
    subresourceRange.levelCount = 1;
    subresourceRange.baseArrayLayer = 0;
    subresourceRange.layerCount = 1;

    VkUtils.SetImageLayout(
      copyCmd,
      _textureImage,
      VkImageLayout.Undefined,
      VkImageLayout.TransferDstOptimal,
      subresourceRange
    );

    vkCmdCopyBufferToImage(
      copyCmd,
      stagingBuffer,
      _textureImage,
      VkImageLayout.TransferDstOptimal,
      1,
      &region
    );

    VkUtils.SetImageLayout(
      copyCmd,
      _textureImage,
      VkImageLayout.TransferDstOptimal,
      VkImageLayout.ShaderReadOnlyOptimal,
      subresourceRange
    );

    _device.FlushCommandBuffer(copyCmd, _device.GraphicsQueue, true);
  }

  private static unsafe void CreateImage(
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
    allocInfo.memoryTypeIndex = device.FindMemoryType(memRequirements.memoryTypeBits, memoryPropertyFlags);

    vkAllocateMemory(device.LogicalDevice, &allocInfo, null, out textureImageMemory).CheckResult();
    vkBindImageMemory(device.LogicalDevice, textureImage, textureImageMemory, 0).CheckResult();
  }

  private static void CreateTextureImageView(VulkanDevice device, VkImage textureImage, out VkImageView imageView) {
    imageView = CreateImageView(device, VkFormat.R8G8B8A8Unorm, textureImage);
  }

  private static unsafe VkImageView CreateImageView(VulkanDevice device, VkFormat format, VkImage textureImage) {
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

  private static unsafe void CreateSampler(VulkanDevice device, out VkSampler imageSampler) {
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

  public virtual unsafe void Dispose(bool disposing) {
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

  public ulong GetSampler() => _imageSampler;
  public ulong GetImageView() => _imageView;
  public ulong GetTextureImage() => _textureImage;
  public int Width => _width;
  public int Height => _height;
  public int Size => _size;
  public string TextureName { get; }
}