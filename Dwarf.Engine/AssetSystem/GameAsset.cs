using System.Numerics;

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;

namespace Dwarf.AssetSystem;
public class GameAsset {
  public Guid AssetId { get; set; }
  public string Name { get; set; }
  public List<string>? AssetResourcePaths { get; set; }
  public List<string>? AssetScripts { get; set; }
  public Vector3 Position { get; set; }
  public Vector3 Rotation { get; set; }
  public Vector3 Scale { get; set; }

  public GameAsset(Guid id, string name) {
    AssetId = id;
    Name = name;
    AssetResourcePaths = [];
    AssetScripts = [];
  }

  public GameAsset(string name) {
    AssetId = Guid.NewGuid();
    Name = name;
    AssetResourcePaths = [];
    AssetScripts = [];
  }

  public void SaveAsset(in Entity entity) {
    GetEntitiyInfo(in entity);
    SerializeAsset();
  }

  public void GetEntitiyInfo(in Entity entity) {
    var transform = entity.TryGetComponent<Transform>();
    if (transform != null) {
      Position = transform.Position;
      Rotation = transform.Rotation;
      Scale = transform.Scale;
    }

    var meshInfo = entity.TryGetComponent<MeshRenderer>();
    if (meshInfo != null) {
      AssetResourcePaths?.Add(meshInfo.FileName);
      if (meshInfo.FileName.Contains(".obj")) {
        for (int i = 0; i < meshInfo.MeshsesCount; i++) {
          var texId = meshInfo.GetTextureIdReference(i);
          var tex = Application.Instance.TextureManager.GetTexture(texId);
          AssetResourcePaths?.Add(tex.TextureName);
        }
      }
    }

    var scripts = entity.GetScripts();
    foreach (var script in scripts) {
      AssetScripts?.Add(script.GetType().Name);
    }
  }

  public void SerializeAsset() {

  }
}
