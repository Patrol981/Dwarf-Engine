using Dwarf.Engine.AbstractionLayer;
using Dwarf.Vulkan;

using StbImageSharp;

namespace Dwarf.Engine;
public class TextureLoader {
  public static async Task<ITexture> LoadFromPath(IDevice device, string path, int flip = 1) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => await VulkanTexture.LoadFromPath((VulkanDevice)device, path, flip),
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
}
