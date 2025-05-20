using System.Diagnostics;
using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;
using glTFLoader.Schema;
using StbImageSharp;
using Vortice.Vulkan;

namespace Dwarf;

public class TextureLoader {
  public static async Task<ITexture> LoadFromPath(object? allocator, IDevice device, string path, int flip = 1) {
    switch (Application.Instance.CurrentAPI) {
      case RenderAPI.Vulkan:
        Debug.Assert(allocator != null);
        return await VulkanTexture.LoadFromPath((VmaAllocator)allocator, (VulkanDevice)device, path, flip);
      default:
        throw new NotImplementedException("Other apis are not currently supported");
    }
  }
  public static async Task<ImageResult> LoadDataFromPath(string path, int flip = 1) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => await VulkanTexture.LoadDataFromPath(path, flip),
      _ => throw new NotImplementedException("Other apis are not currently supported"),
    };
  }
  public static ImageResult LoadDataFromBytes(byte[] data, int flip = 1) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => VulkanTexture.LoadDataFromBytes(data, flip),
      _ => throw new NotImplementedException("Other apis are not currently supported"),
    };
  }

  public static ITexture LoadFromBytes(object? allocator, IDevice device, byte[] data, string textureName, int flip = 1) {
    switch (Application.Instance.CurrentAPI) {
      case RenderAPI.Vulkan:
        Debug.Assert(allocator != null);
        return VulkanTexture.LoadFromBytes(
          (VmaAllocator)allocator,
          (VulkanDevice)device,
          data,
          textureName,
          flip
        );
      default:
        throw new NotImplementedException("Other apis are not currently supported");
    }
  }

  public static ITexture LoadFromGLTF(
    object? allocator,
    IDevice device,
    in Gltf gltf,
    in byte[] globalBuffer,
    Image gltfImage,
    string textureName,
    TextureSampler textureSampler,
    int flip
  ) {
    switch (Application.Instance.CurrentAPI) {
      case RenderAPI.Vulkan:
        Debug.Assert(allocator != null);
        return VulkanTexture.LoadFromGLTF(
          (VmaAllocator)allocator,
          device,
          gltf,
          globalBuffer,
          gltfImage,
          textureName,
          textureSampler,
          flip
        );
      default:
        throw new NotImplementedException("Other apis are not currently supported");
    }
  }
}
