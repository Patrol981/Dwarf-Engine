using Dwarf.Engine;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dwarf.AssetSystem;
public class SceneAsset {
  public string Name { get; set; }
  public List<GameAsset> GameAssets { get; set; }

  public SceneAsset() {
    Name = string.Empty;
    GameAssets = new List<GameAsset>();
  }

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

  public void SaveSceneAsset() {
    var serializer = new SerializerBuilder()
      .WithNamingConvention(PascalCaseNamingConvention.Instance)
      .Build();

    var yaml = serializer.Serialize(this);
    Directory.CreateDirectory("./Resources/Assets");

    File.WriteAllText($"./Resources/Assets/{Name}.scene", yaml);
  }

  public async static Task<SceneAsset> LoadSceneAsset(Application application, string sceneName) {
    var text = File.ReadAllText($"./Resources/Assets/{sceneName}.scene");

    var desrializer = new DeserializerBuilder()
      .WithNamingConvention(PascalCaseNamingConvention.Instance)
      .Build();

    var scene = desrializer.Deserialize<SceneAsset>(text);
    var sceneAsset = new SceneAsset(scene.Name);
    sceneAsset.GameAssets = scene.GameAssets;

    foreach (var gameAsset in scene.GameAssets) {
      application.AddEntity(await gameAsset.CreateEntityBasedOnInfo());
    }

    return sceneAsset;
  }
}
