using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

namespace Dwarf.Engine;

public class TextureManager : IDisposable {
  private readonly Device _device;
  private Dictionary<Guid, Texture> _loadedTextures;

  public TextureManager(Device device) {
    _device = device;
    _loadedTextures = new();
  }

  public void AddRange(Texture[] textures) {
    for (int i = 0; i < textures.Length; i++) {
      _loadedTextures.Add(Guid.NewGuid(), textures[i]);
    }
  }

  public Task AddTexture(string texturePath) {
    foreach (var tex in _loadedTextures) {
      if (tex.Value.TextureName == texturePath) {
        Logger.Warn($"Texture [{texturePath}] is already loaded. Skipping current add call.");
        return Task.CompletedTask;
      }
    }
    _loadedTextures.Add(Guid.NewGuid(), new Texture(_device, texturePath));
    return Task.CompletedTask;
  }

  public Task AddTextureFromLocal(string textureName) {
    var basePath = "./Textures/";
    var finalPath = Path.Combine(basePath, textureName);
    foreach (var tex in _loadedTextures) {
      if (tex.Value.TextureName == finalPath) {
        Logger.Warn($"Texture [{finalPath}] is already loaded. Skipping current add call.");
        return Task.CompletedTask;
      }
    }
    _loadedTextures.Add(Guid.NewGuid(), new Texture(_device, finalPath, true));
    return Task.CompletedTask;
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

  public void Dispose() {
    foreach (var tex in _loadedTextures) {
      RemoveTexture(tex.Key);
    }
  }
}