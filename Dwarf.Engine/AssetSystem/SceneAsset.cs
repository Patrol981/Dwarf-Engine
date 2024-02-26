namespace Dwarf.AssetSystem;
public class SceneAsset {
  public string Name { get; set; }
  public List<GameAsset> GameAssets { get; set; }

  public SceneAsset(string name, List<GameAsset> gameAssets) {
    Name = name;
    GameAssets = gameAssets;
  }

  public SceneAsset(string name) {
    Name = name;
    GameAssets = [];
  }

  public void AddGameAsset(GameAsset gameAsset) {
    GameAssets.Add(gameAsset);
  }
}
