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
  internal VkImage _textureImage = VkImage.Null;
  internal VkDeviceMemory _textureImageMemory = VkDeviceMemory.Null;
  // private Vulkan.Buffer _textureBuffer = null!;
  internal VkImageView _imageView = VkImageView.Null;
  internal VkSampler _imageSampler = VkSampler.Null;

  private Mutex _mutex = new();

  public Texture(Device device, string texturePath, bool createOnStart = false) {
    TextureName = texturePath;
    _device = device;
    if (createOnStart) {
      CreateTexture(_device, texturePath, out _textureImage, out _textureImageMemory, out _imageView, out _imageSampler);
    }
  }

  public static Texture[] InitTextures(ref Device device, ReadOnlySpan<string> texturePaths) {
    var textures = new Texture[texturePaths.Length];
    for (int i = 0; i < textures.Length; i++) {
      textures[i] = new(device, texturePaths[i], false);
    }

    return textures;
  }

  public unsafe static void CreateTexture(
    Device device,
    string texturePath,
    out VkImage textureImage,
    out VkDeviceMemory textureMemory,
    out VkImageView imageView,
    out VkSampler sampler,
    int flip = 1
  ) {
    var startTime = DateTime.Now;
    // Console.WriteLine("Start");
    StbImage.stbi_set_flip_vertically_on_load(flip);

    using var stream = File.OpenRead($"{texturePath}");
    var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
    stream.Dispose();

    // Console.WriteLine("Map");

    // This takes the longest
    var size = img.Width * img.Height * 4;
    var stagingBuffer = new Vulkan.Buffer(
      device,
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

    // Console.WriteLine("Create Image");
    CreateImage(
      ref device,
      (uint)img.Width,
      (uint)img.Height,
      VkFormat.R8G8B8A8Srgb,
      VkImageTiling.Optimal,
      VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
      VkMemoryPropertyFlags.DeviceLocal,
      out textureImage,
      out textureMemory
    );

    // Console.WriteLine("Create Transitions");
    CreateImageTransitions(device, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, textureImage);
    CopyBufferToImage(device, stagingBuffer.GetBuffer(), textureImage, img.Width, img.Height);
    CreateImageTransitions(device, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal, textureImage);

    //transitionImageLayout(textureImage, VK_FORMAT_R8G8B8A8_SRGB, VK_IMAGE_LAYOUT_UNDEFINED, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL);
    //copyBufferToImage(stagingBuffer, textureImage, static_cast<uint32_t>(texWidth), static_cast<uint32_t>(texHeight));
    //transitionImageLayout(textureImage, VK_FORMAT_R8G8B8A8_SRGB, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

    stagingBuffer.Dispose();

    //vkDestroyBuffer(device, stagingBuffer, nullptr);
    //vkFreeMemory(device, stagingBufferMemory, nullptr);
    // Console.WriteLine("Creating View");
    CreateTextureImageView(device, textureImage, out imageView);
    // Console.WriteLine("Create Sampler");
    CreateSampler(device, out sampler);

    // _textureBuffer.Dispose();
    var endTime = DateTime.Now;
    Logger.Warn($"[CREATE TEXTURE TIME]: {(endTime - startTime).TotalMilliseconds}");
  }

  private unsafe static void CreateImage(
    ref Device device,
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
    // imageInfo.sType = VkStructureType.ImageCreateInfo;
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

    device._mutex.WaitOne();
    vkCmdCopyBufferToImage(commandBuffer, buffer, image, VkImageLayout.TransferDstOptimal, 1, &region);
    device._mutex.ReleaseMutex();

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
    // barrier.sType = VkStructureType.ImageMemoryBarrier;
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
    // viewInfo.sType = VkStructureType.ImageViewCreateInfo;
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
    // samplerInfo.sType = VkStructureType.SamplerCreateInfo;
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
    // _textureBuffer.Dispose();
  }

  // public Vulkan.Buffer GetBuffer() => _textureBuffer;
  public VkSampler GetSampler() => _imageSampler;
  public VkImageView GetImageView() => _imageView;
  public VkImage GetTextureImage() => _textureImage;
}