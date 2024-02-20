using Dwarf.Engine;

namespace Dwarf.AssetSystem;
public class MaterialAsset : GameAsset {
  public Material Material { get; private set; }

  public MaterialAsset(Guid id, string name, Material material) : base(id, name, null!) {
    Material = material;
  }

  public MaterialAsset(string name, Material material) : base(name, null!) {
    Material = material;
  }
}
