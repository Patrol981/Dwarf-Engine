namespace Dwarf.Engine.AbstractionLayer;
public interface ITexture : IDisposable {
  public string TextureName { get; }

  public void SetTextureData(nint dataPtr);
  public void SetTextureData(byte[] data);

  // public Task<ITexture> LoadFromPath(IDevice device, string path, int flip);
  // public Task<ImageResult> LoadDataFromPath(string path, int flip = 1);
  // public ImageResult LoadDataFromBytes(byte[] data, int flip = 1);

  public ulong Sampler { get; }
  public ulong ImageView { get; }
  public ulong TextureImage { get; }
  public ulong TextureDescriptor { get; }
  public void BuildDescriptor(nint descriptorSetLayout, nint descriptorPool);
  public int Width { get; }
  public int Height { get; }
  public int Size { get; }
}
