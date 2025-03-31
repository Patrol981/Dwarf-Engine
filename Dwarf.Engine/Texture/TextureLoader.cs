using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;
using glTFLoader.Schema;
using StbImageSharp;
using Vortice.Vulkan;

namespace Dwarf;
public class TextureLoader {
  public static async Task<ITexture> LoadFromPath(VmaAllocator vmaAllocator, IDevice device, string path, int flip = 1) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => await VulkanTexture.LoadFromPath(vmaAllocator, (VulkanDevice)device, path, flip),
      _ => throw new NotImplementedException("Other apis are not currently supported"),
    };
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

  public static ITexture LoadFromBytes(VmaAllocator vmaAllocator, IDevice device, byte[] data, string textureName, int flip = 1) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => VulkanTexture.LoadFromBytes(vmaAllocator, (VulkanDevice)device, data, textureName, flip),
      _ => throw new NotImplementedException("Other apis are not currently supported"),
    };
  }
}
