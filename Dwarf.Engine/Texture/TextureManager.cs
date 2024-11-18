using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.UI;
using Dwarf.Vulkan;
namespace Dwarf;

public class TextureManager : IDisposable {
  private readonly VulkanDevice _device;

  public TextureManager(VulkanDevice device) {
    _device = device;
    LoadedTextures = [];
    TextureArray = [];
  }

  public void AddRange(ITexture[] textures) {
    for (int i = 0; i < textures.Length; i++) {
      LoadedTextures.Add(Guid.NewGuid(), textures[i]);
    }
  }

  public async Task<Task> AddTextureArray(string textureName, params string[] paths) {
    var baseData = await VulkanTextureArray.LoadDataFromPath(paths[0]);
    var id = Guid.NewGuid();
    var texture = new VulkanTextureArray(_device, baseData.Width, baseData.Height, paths, textureName);
    TextureArray.TryAdd(id, texture);

    return Task.CompletedTask;
  }

  public async Task<Task> AddTexture(string texturePath, int flip = 1) {
    foreach (var tex in LoadedTextures) {
      if (tex.Value.TextureName == texturePath) {
        Logger.Warn($"Texture [{texturePath}] is already loaded. Skipping current add call.");
        return Task.CompletedTask;
      }
    }
    var texture = await TextureLoader.LoadFromPath(_device, texturePath, flip);
    LoadedTextures.Add(Guid.NewGuid(), texture);
    return Task.CompletedTask;
  }

  public Guid AddTexture(ITexture texture) {
    foreach (var tex in LoadedTextures) {
      if (tex.Value.TextureName == texture.TextureName) {
        Logger.Warn($"Texture [{texture.TextureName}] is already loaded. Skipping current add call.");
        return tex.Key;
      }
    }
    var guid = Guid.NewGuid();
    LoadedTextures.Add(guid, texture);
    return guid;
  }

  public bool TextureExists(ITexture texture) {
    foreach (var tex in LoadedTextures) {
      if (tex.Value.TextureName == texture.TextureName) {
        return true;
      }
    }

    return false;
  }

  public static async Task<ITexture[]> AddTextures(IDevice device, string[] paths, int flip = 1) {
    var textures = new ITexture[paths.Length];
    for (int i = 0; i < textures.Length; i++) {
      textures[i] = await TextureLoader.LoadFromPath(device, paths[i], flip);
    }
    return textures;
  }

  public static ITexture[] AddTextures(IDevice device, List<byte[]> bytes, string[] nameTags) {
    var textures = new ITexture[bytes.Count];
    for (int i = 0; i < bytes.Count; i++) {
      var imgData = TextureLoader.LoadDataFromBytes(bytes[i]);
      _ = Application.Instance.CurrentAPI switch {
        RenderAPI.Vulkan => textures[i] = new VulkanTexture((VulkanDevice)device, imgData.Width, imgData.Height, nameTags[i]),
        _ => throw new NotImplementedException(),
      };
      textures[i].SetTextureData(imgData.Data);
    }
    return textures;
  }

  public void RemoveTexture(Guid key) {
    LoadedTextures[key].Dispose();
    LoadedTextures.Remove(key);
  }

  public void RemoveTextureArray(Guid key) {
    TextureArray[key].Dispose();
    TextureArray.Remove(key);
  }

  public ITexture GetTexture(Guid key) {
    return LoadedTextures.GetValueOrDefault(key) ?? null!;
  }

  public Guid GetTextureId(string textureName) {
    foreach (var tex in LoadedTextures) {
      if (tex.Value.TextureName == textureName) {
        return tex.Key;
      }
    }
    return Guid.Empty;
  }

  public Dictionary<Guid, ITexture> LoadedTextures { get; }
  public Dictionary<Guid, VulkanTextureArray> TextureArray { get; }

  public void Dispose() {
    foreach (var tex in LoadedTextures) {
      RemoveTexture(tex.Key);
    }
    foreach (var tex in TextureArray) {
      RemoveTextureArray(tex.Key);
    }
  }
}