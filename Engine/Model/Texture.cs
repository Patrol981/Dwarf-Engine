using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Vulkan;
using StbImageSharp;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Texture : IDisposable {
  private readonly Device _device = null!;
  private VkImage _textureImage = VkImage.Null;
  private VkDeviceMemory _textureMemory = VkDeviceMemory.Null;
  private Vulkan.Buffer _textureBuffer = null!;

  public Texture(Device device) {
    _device = device;
  }

  public unsafe void CreateTexture(string texturePath, int flip = 1) {
    StbImage.stbi_set_flip_vertically_on_load(flip);

    using var stream = File.OpenRead($"{texturePath}");
    var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
    stream.Dispose();

    var size = img.Width * img.Height * 4;
    var buffer = new Vulkan.Buffer(
      _device,
      (ulong)(size),
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    buffer.Map();
    buffer.WriteToBuffer(Utils.ToIntPtr(img.Data), (ulong)size);

    CreateImage(
      (uint)img.Width,
      (uint)img.Height,
      VkFormat.R8G8B8A8Srgb,
      VkImageTiling.Optimal,
      VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _textureBuffer = new Vulkan.Buffer(
      _device,
      (ulong)size,
      VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(buffer.GetBuffer(), _textureBuffer.GetBuffer(), (ulong)size);
    buffer.Dispose();
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

    vkAllocateMemory(_device.LogicalDevice, &allocInfo, null, out _textureMemory).CheckResult();
    vkBindImageMemory(_device.LogicalDevice, _textureImage, _textureMemory, 0).CheckResult();
  }

  public unsafe void Dispose() {
    vkFreeMemory(_device.LogicalDevice, _textureMemory);
    vkDestroyImage(_device.LogicalDevice, _textureImage);
    _textureBuffer.Dispose();
  }

  public Vulkan.Buffer GetBuffer() => _textureBuffer;
}