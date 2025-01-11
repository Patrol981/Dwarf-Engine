using Dwarf.AbstractionLayer;
using Dwarf.Math;
using Dwarf.Utils;
using Dwarf.Vulkan;

using glTFLoader.Schema;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

public class VulkanTexture : ITexture {
  protected readonly VulkanDevice _device = null!;
  protected readonly VmaAllocator _vmaAllocator;

  public int TextureIndex { get; set; } = -1;
  public struct VulkanTextureData {
    public VkImage TextureImage;
    public VkDeviceMemory TextureImageMemory;
    public VkImageView ImageView;
    public VkSampler ImageSampler;
  }

  public struct TextureSampler {
    public VkFilter MagFilter;
    public VkFilter MinFilter;
    public VkSamplerAddressMode AddressModeU;
    public VkSamplerAddressMode AddressModeV;
    public VkSamplerAddressMode AddressModeW;
  };

  internal VulkanTextureData _textureSampler;

  protected int _width = 0;
  protected int _height = 0;
  protected int _size = 0;

  protected VkDescriptorSet _textureDescriptor = VkDescriptorSet.Null;

  public VulkanTexture(VmaAllocator vmaAllocator, VulkanDevice device, int width, int height, string textureName = "") {
    _device = device;
    _vmaAllocator = vmaAllocator;
    _width = width;
    _height = height;
    TextureName = textureName;

    _size = _width * _height * 4;
  }

  public VulkanTexture(VmaAllocator vmaAllocator, VulkanDevice device, int size, int width, int height, string textureName = "") {
    _device = device;
    _vmaAllocator = vmaAllocator;
    TextureName = textureName;
    _size = size;
    _width = width;
    _height = height;
  }

  public void SetTextureData(nint dataPtr) {
    SetTextureData(dataPtr, VkImageCreateFlags.None);
  }
  private void SetTextureData(nint dataPtr, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    var stagingBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      (ulong)_size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(dataPtr, (ulong)_size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer, createFlags);
  }

  public void SetTextureData(byte[] data) {
    SetTextureData(data, VkImageCreateFlags.None);
  }

  private void SetTextureData(byte[] data, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    var stagingBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      (ulong)_size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map();

    TextureData = data;
    unsafe {
      fixed (byte* dataPtr = data) {
        stagingBuffer.WriteToBuffer((nint)dataPtr, (ulong)_size);
      }
    }

    stagingBuffer.Unmap();
    ProcessTexture(stagingBuffer, createFlags);
  }

  public void SetTextureData(int[,] data, Vector4I frontColor, Vector4I backColor, int scale) {
    int width = data.GetLength(0);
    int height = data.GetLength(1);
    int scaledWidth = width * scale;
    int scaledHeight = height * scale;

    byte[] rgbaData = new byte[scaledWidth * scaledHeight * 4];

    for (int y = 0; y < height; y++) {
      for (int x = 0; x < width; x++) {
        bool isPainted = data[x, y] == 1;
        var r = isPainted ? (byte)frontColor.X : (byte)backColor.X;
        var g = isPainted ? (byte)frontColor.Y : (byte)backColor.Y;
        var b = isPainted ? (byte)frontColor.Z : (byte)backColor.Z;
        var a = isPainted ? (byte)frontColor.W : (byte)backColor.W;

        for (int dy = 0; dy < scale; dy++) {
          for (int dx = 0; dx < scale; dx++) {
            int scaledX = x * scale + dx;
            int scaledY = y * scale + dy;
            int index = (scaledY * scaledWidth + scaledX) * 4;

            rgbaData[index] = r;
            rgbaData[index + 1] = g;
            rgbaData[index + 2] = b;
            rgbaData[index + 3] = a;
          }
        }
      }
    }

    SetTextureData(rgbaData);
  }

  public void BuildDescriptor(nint descriptorSetLayout, nint descriptorPool) {
    VkDescriptorImageInfo imageInfo = new() {
      sampler = _textureSampler.ImageSampler,
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = _textureSampler.ImageView
    };
    unsafe {
      _ = new VulkanDescriptorWriter(descriptorSetLayout, descriptorPool)
      .WriteImage(0, &imageInfo)
      .Build(out _textureDescriptor);
    }
  }

  public unsafe void BuildDescriptor(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    VkDescriptorSet descriptorSet = new();

    VkDescriptorImageInfo descriptorImageInfo = new() {
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = ImageView
    };
    VkDescriptorImageInfo descriptorSamplerInfo = new() {
      sampler = Sampler
    };

    var allocInfo = new VkDescriptorSetAllocateInfo();
    allocInfo.descriptorPool = descriptorPool.GetVkDescriptorPool();
    allocInfo.descriptorSetCount = 1;
    var setLayout = descriptorSetLayout.GetDescriptorSetLayout();
    allocInfo.pSetLayouts = &setLayout;
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkWriteDescriptorSet* writes = stackalloc VkWriteDescriptorSet[2];

    writes[0] = new VkWriteDescriptorSet() {
      descriptorType = VkDescriptorType.SampledImage,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo,
      descriptorCount = 1,
      dstSet = descriptorSet
    };

    writes[1] = new VkWriteDescriptorSet() {
      descriptorType = VkDescriptorType.Sampler,
      dstBinding = 1,
      descriptorCount = 1,
      pImageInfo = &descriptorSamplerInfo,
      dstSet = descriptorSet
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writes, 0, null);

    _textureDescriptor = descriptorSet;

    //unsafe {
    //  _ = new VulkanDescriptorWriter(descriptorSetLayout, descriptorPool)
    //  .WriteImage(0, &imageInfo)
    //  .WriteSampler(1, Image)
    //  .Build(out _textureDescriptor);
    //}
  }

  public unsafe void AddDescriptor(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    if (descriptorSetLayout.GetDescriptorSetLayout().IsNull) throw new ArgumentException("Layout is null");

    VkDescriptorSet descriptorSet = new();

    var allocInfo = new VkDescriptorSetAllocateInfo();
    allocInfo.descriptorPool = descriptorPool.GetVkDescriptorPool();
    allocInfo.descriptorSetCount = 1;
    var setLayout = descriptorSetLayout.GetDescriptorSetLayout();
    allocInfo.pSetLayouts = &setLayout;
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkDescriptorImageInfo descriptorImageInfo = new() {
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = ImageView
    };
    VkDescriptorImageInfo descriptorSamplerInfo = new() {
      sampler = Sampler
    };

    VkWriteDescriptorSet* writes = stackalloc VkWriteDescriptorSet[2];

    writes[0] = new VkWriteDescriptorSet() {
      descriptorType = VkDescriptorType.SampledImage,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo,
      descriptorCount = 1,
      dstSet = descriptorSet
    };

    writes[1] = new VkWriteDescriptorSet() {
      descriptorType = VkDescriptorType.Sampler,
      dstBinding = 1,
      descriptorCount = 1,
      pImageInfo = &descriptorSamplerInfo,
      dstSet = descriptorSet
    };


    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writes, 0, null);

    //VkDescriptorImageInfo descImage = new();
    //VkWriteDescriptorSet writeDesc = new();

    //descImage.sampler = _textureSampler.ImageSampler;
    //descImage.imageView = _textureSampler.ImageView;
    //descImage.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;

    //writeDesc.dstSet = descriptorSet;
    //writeDesc.descriptorCount = 1;
    //writeDesc.descriptorType = VkDescriptorType.SampledImage;
    //writeDesc.pImageInfo = &descImage;

    //vkUpdateDescriptorSets(_device.LogicalDevice, writeDesc);

    _textureDescriptor = descriptorSet;
  }

  private void ProcessTexture(DwarfBuffer stagingBuffer, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    Application.Instance.Mutex.WaitOne();
    unsafe {
      if (_textureSampler.TextureImage.IsNotNull) {
        _device.WaitDevice();
        vkDestroyImage(_device.LogicalDevice, _textureSampler.TextureImage);
      }

      if (_textureSampler.TextureImageMemory.IsNotNull) {
        _device.WaitDevice();
        vkFreeMemory(_device.LogicalDevice, _textureSampler.TextureImageMemory);
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
      out _textureSampler.TextureImage,
      out _textureSampler.TextureImageMemory
    );


    HandleTexture(stagingBuffer.GetBuffer(), VkFormat.R8G8B8A8Unorm, _width, _height);

    CreateTextureImageView(_device, _textureSampler.TextureImage, out _textureSampler.ImageView);


    CreateSampler(_device, out _textureSampler.ImageSampler);

    stagingBuffer.Dispose();
    Application.Instance.Mutex.ReleaseMutex();
  }

  public static async Task<ITexture> LoadFromPath(VmaAllocator vmaAllocator, VulkanDevice device, string path, int flip = 1, VkImageCreateFlags imageCreateFlags = VkImageCreateFlags.None) {
    ImageResult textureData;
    if (Path.Exists(path)) {
      textureData = await LoadDataFromPath(path, flip);
    } else {
      var cwd = DwarfPath.AssemblyDirectory;
      var pathResult = Path.Combine(cwd, path);
      textureData = await LoadDataFromPath(pathResult, flip);
    }

    var texture = new VulkanTexture(vmaAllocator, device, textureData.Width, textureData.Height, path);
    texture.SetTextureData(textureData.Data, imageCreateFlags);
    return texture;
  }

  public static ITexture LoadFromBytes(
    VmaAllocator vmaAllocator,
    VulkanDevice device,
    byte[] data,
    string textureName,
    int flip = 1
  ) {
    var texInfo = LoadDataFromBytes(data, flip);
    var texture = new VulkanTexture(vmaAllocator, device, texInfo.Width, texInfo.Height, textureName);
    texture.SetTextureData(texInfo.Data);
    return texture;
  }

  public static ITexture LoadFromBytesDirect(
    VmaAllocator vmaAllocator,
    VulkanDevice device,
    byte[] data,
    int size,
    int width,
    int height,
    string textureName
  ) {
    var texture = new VulkanTexture(vmaAllocator, device, size, width, height, textureName);
    texture.SetTextureData(data);
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

  public static ITexture LoadFromGLTF(
    VmaAllocator vmaAllocator,
    IDevice device,
    Gltf gltf,
    byte[] globalBuffer,
    glTFLoader.Schema.Image gltfImage,
    string textureName,
    TextureSampler textureSampler,
    int flip = 1
  ) {
    bool isKtx2 = false;
    if (gltfImage.Uri != null) {
      if (gltfImage.Uri.Contains("ktx2")) {
        isKtx2 = true;
      }
    }


    if (isKtx2) {
      throw new NotImplementedException("KTX2 is not currently supported");
    } else {
      var bufferView = gltf.BufferViews[gltfImage.BufferView!.Value];
      var buffer = gltf.Buffers[bufferView.Buffer];

      using var stream = new MemoryStream(globalBuffer, bufferView.ByteOffset, bufferView.ByteLength);
      using var reader = new BinaryReader(stream);

      byte[] imgData = reader.ReadBytes(buffer.ByteLength);

      // var path = Path.Combine(DwarfPath.AssemblyDirectory, $"{textureName}_{Guid.NewGuid()}.png");
      // File.WriteAllBytes(path, imgData);

      StbImage.stbi_set_flip_vertically_on_load(flip);
      var image = ImageResult.FromMemory(imgData);
      byte[] rgba;
      try {
        rgba = ConvertRGBToRGBA(image.Data);
      } catch {
        rgba = image.Data;
      }

      var texture = new VulkanTexture(vmaAllocator, (VulkanDevice)device, image.Width, image.Height, textureName);
      texture.SetTextureData(rgba);

      return texture;
    }
  }

  public static byte[] ConvertRGBToRGBA(byte[] imgData) {
    if (imgData == null || imgData.Length % 3 != 0)
      throw new ArgumentException("Input byte array length must be a multiple of 3.", nameof(imgData));

    int pixelCount = imgData.Length / 3;
    byte[] rgbaData = new byte[pixelCount * 4];
    for (int i = 0, j = 0; i < imgData.Length; i += 3, j += 4) {
      rgbaData[j] = imgData[i];       // Red
      rgbaData[j + 1] = imgData[i + 1]; // Green
      rgbaData[j + 2] = imgData[i + 2]; // Blue
      rgbaData[j + 3] = 255;            // Alpha (fully opaque)
    }

    return rgbaData;
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
      _textureSampler.TextureImage,
      VkImageLayout.Undefined,
      VkImageLayout.TransferDstOptimal,
      subresourceRange
    );

    vkCmdCopyBufferToImage(
      copyCmd,
      stagingBuffer,
      _textureSampler.TextureImage,
      VkImageLayout.TransferDstOptimal,
      1,
      &region
    );

    VkUtils.SetImageLayout(
      copyCmd,
      _textureSampler.TextureImage,
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

    vkCreateSampler(device.LogicalDevice, &samplerInfo, null, out imageSampler).CheckResult();
  }

  public virtual unsafe void Dispose(bool disposing) {
    if (disposing) {
      vkFreeMemory(_device.LogicalDevice, _textureSampler.TextureImageMemory);
      vkDestroyImage(_device.LogicalDevice, _textureSampler.TextureImage);
      vkDestroyImageView(_device.LogicalDevice, _textureSampler.ImageView);
      vkDestroySampler(_device.LogicalDevice, _textureSampler.ImageSampler);
    }
  }

  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  public ulong Sampler => _textureSampler.ImageSampler;
  public ulong ImageView => _textureSampler.ImageView;
  public ulong TextureImage => _textureSampler.TextureImage;
  public ulong TextureDescriptor => _textureDescriptor;
  public VkDescriptorSet VkTextureDescriptor => _textureDescriptor;
  public int Width => _width;
  public int Height => _height;
  public int Size {
    get => _size;
    set => _size = value;
  }
  public byte[] TextureData { get; private set; } = [];
  public string TextureName { get; }
}