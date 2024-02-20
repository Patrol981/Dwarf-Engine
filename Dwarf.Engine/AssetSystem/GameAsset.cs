namespace Dwarf.AssetSystem;
public abstract class GameAsset {
  public Guid AssetId { get; private set; }
  public string Name { get; private set; }
  public List<string>? AssetResourcePaths { get; private set; }

  public GameAsset(Guid id, string name, List<string> assetResourcePaths) {
    AssetId = id;
    Name = name;
    AssetResourcePaths = assetResourcePaths;
  }

  public GameAsset(string name, List<string> assetResourcePaths) {
    AssetId = Guid.NewGuid();
    Name = name;
    AssetResourcePaths = assetResourcePaths;
  }
}
