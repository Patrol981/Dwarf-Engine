using Dwarf.Vulkan;

namespace Dwarf.Engine;

public class TextureThread {
  private readonly Device _device;
  private Texture[] _textures;
  private string[] _paths;

  public TextureThread(ref Device device, ref Texture[] textures, string[] paths) {
    _device = device;
    _textures = textures;
    _paths = paths;
  }


  public void Process() {
    for (int i = 0; i < _textures.Length; i++) {
      Texture.CreateTexture(
        _device,
        _paths[i],
        out _textures[i]._textureImage,
        out _textures[i]._textureImageMemory,
        out _textures[i]._imageView,
        out _textures[i]._imageSampler
      );
    }
  }
}