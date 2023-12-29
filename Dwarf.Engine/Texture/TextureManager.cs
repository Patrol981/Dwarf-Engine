using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

namespace Dwarf.Engine;

public class TextureManager : IDisposable {
  private readonly Device _device;
  private Dictionary<Guid, Texture> _loadedTextures;
  private readonly FreeType _ft;

  public TextureManager(Device device) {
    _device = device;
    _loadedTextures = [];

    _ft = new FreeType(device);
    _ft.Init();

    foreach (var c in _ft.Characters) {
      AddTexture(c.Value.Texture);
    }
  }

  public void AddRange(Texture[] textures) {
    for (int i = 0; i < textures.Length; i++) {
      _loadedTextures.Add(Guid.NewGuid(), textures[i]);
    }
  }

  public async Task<Task> AddTexture(string texturePath) {
    foreach (var tex in _loadedTextures) {
      if (tex.Value.TextureName == texturePath) {
        Logger.Warn($"Texture [{texturePath}] is already loaded. Skipping current add call.");
        return Task.CompletedTask;
      }
    }
    var texture = await Texture.LoadFromPath(_device, texturePath);
    _loadedTextures.Add(Guid.NewGuid(), texture);
    return Task.CompletedTask;
  }

  public Task AddTexture(Texture texture) {
    foreach (var tex in _loadedTextures) {
      if (tex.Value.TextureName == texture.TextureName) {
        Logger.Warn($"Texture [{texture.TextureName}] is already loaded. Skipping current add call.");
        return Task.CompletedTask;
      }
    }
    _loadedTextures.Add(Guid.NewGuid(), texture);
    return Task.CompletedTask;
  }

  public static async Task<Texture[]> AddTextures(Device device, string[] paths, int flip = 1) {
    var textures = new Texture[paths.Length];
    for (int i = 0; i < textures.Length; i++) {
      textures[i] = await Texture.LoadFromPath(device, paths[i], flip);
    }
    return textures;
  }

  public static Texture[] AddTextures(Device device, List<byte[]> bytes, string[] nameTags) {
    var textures = new Texture[bytes.Count];
    for (int i = 0; i < bytes.Count; i++) {
      var imgData = Texture.LoadDataFromBytes(bytes[i]);
      textures[i] = new Texture(device, imgData.Width, imgData.Height, nameTags[i]);
      textures[i].SetTextureData(imgData.Data);
    }
    return textures;
  }

  public void RemoveTexture(Guid key) {
    _loadedTextures[key].Dispose();
    _loadedTextures.Remove(key);
  }

  public Texture GetTexture(Guid key) {
    return _loadedTextures.GetValueOrDefault(key) ?? null!;
  }

  public Guid GetTextureId(string textureName) {
    foreach (var tex in _loadedTextures) {
      if (tex.Value.TextureName == textureName) {
        return tex.Key;
      }
    }
    return Guid.Empty;
  }

  public Dictionary<Guid, Texture> LoadedTextures => _loadedTextures;
  public FreeType FreeType => _ft;

  public void Dispose() {
    foreach (var tex in _loadedTextures) {
      RemoveTexture(tex.Key);
    }
  }
}