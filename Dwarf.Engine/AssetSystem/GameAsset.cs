using System.Numerics;

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Procedural;
using Dwarf.Engine.Rendering.UI;

namespace Dwarf.AssetSystem;

public struct GameAssetRigidbodyData {
  public Vector3 Size;
  public Vector3 Offset;
  public bool IsKinematic;
  public PrimitiveType PrimitiveType;
  public bool Flip;
}

public struct CameraAssetData {
  public float Fov;
  public float CameraSpeed;
  public float Pitch;
  public float Yaw;

  public Guid LookAtTarget;
}

public class GameAsset {
  public Guid AssetId { get; set; }
  public string Name { get; set; }
  public List<string>? AssetResourcePaths { get; set; }
  public List<string>? AssetScripts { get; set; }
  public string AssetType { get; set; }
  public Vector3 Position { get; set; }
  public Vector3 Rotation { get; set; }
  public Vector3 Scale { get; set; }
  public GameAssetRigidbodyData? RigidbodyData { get; set; }
  public CameraAssetData? CameraAsset { get; set; } = null;
  public int TextureFlip = 1;

  public GameAsset() {
  }

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
      TextureFlip = meshInfo.TextureFlipped;
      AssetType = meshInfo.GetType().FullName!.ToString();
    }

    var terrain = entity.TryGetComponent<Terrain3D>();
    if (terrain != null) {
      AssetType = terrain.GetType().FullName!.ToString();
    }

    var canvas = entity.TryGetComponent<Canvas>();
    if (canvas != null) {
      AssetType = canvas.GetType().FullName!.ToString();
    }

    var camera = entity.TryGetComponent<Camera>();
    if (camera != null) {
      AssetType = camera.GetType().FullName!.ToString();

      CameraAsset = new() {
        Yaw = camera.Yaw,
        Pitch = camera.Pitch,
        Fov = camera.RawFov,
        CameraSpeed = CameraState.GetCameraSpeed(),

        LookAtTarget = entity.HasComponent<ThirdPersonCamera>() ? entity.GetComponent<ThirdPersonCamera>().FollowTarget.EntityID : Guid.Empty,
      };
    }

    var rigidbody = entity.TryGetComponent<Rigidbody>();
    if (rigidbody != null) {
      // size
      // offset
      // primitive type
      // kinematic
      // flip

      RigidbodyData = new() {
        Size = rigidbody.Size,
        Offset = rigidbody.Offset,
        PrimitiveType = rigidbody.PrimitiveType,
        IsKinematic = rigidbody.Kinematic,
        Flip = rigidbody.Flipped
      };
    }

    var scripts = entity.GetScripts();
    foreach (var script in scripts) {
      AssetScripts?.Add(script.GetType().AssemblyQualifiedName!);
    }
  }

  public async Task<Entity> CreateEntityBasedOnInfo() {
    var entity = new Entity(AssetId);
    entity.Name = Name;

    entity.AddTransform(Position, Rotation, Scale);

    // check for model data
    await HandleAssetResources(entity);
    HandleComponents(entity);
    HandleRigidbody(entity);
    HandleScripts(in entity);
    HandleCamera(entity);

    return entity;
  }

  private async Task<Task> HandleAssetResources(Entity entity) {
    if (AssetResourcePaths == null) return Task.CompletedTask;
    if (AssetResourcePaths.Count < 1) return Task.CompletedTask;

    var glb = AssetResourcePaths?.Where(x => x.Contains(".glb")).FirstOrDefault();
    if (glb != null) {
      entity.AddMaterial();
      entity.AddModel(glb, TextureFlip);
    }

    var obj = AssetResourcePaths?.Where(x => x.Contains(".obj")).FirstOrDefault();
    if (obj != null) {
      // entity.AddModel(obj);
      entity.AddMaterial();
      var app = Application.Instance;
      entity.AddComponent(
        await new GenericLoader().LoadModelOptimized(app.Device, app.Renderer, obj)
      );

      if (AssetResourcePaths?.Count > 2) {
        var textures = AssetResourcePaths;
        textures.Remove(obj);
        entity.GetComponent<MeshRenderer>().BindMultipleModelPartsToTextures(app.TextureManager, textures.ToArray());
      } else {
        entity.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, AssetResourcePaths![1]);
      }
    }

    return Task.CompletedTask;
  }

  private void HandleScripts(in Entity entity) {
    if (AssetScripts == null) return;
    if (AssetScripts.Count < 1) return;

    foreach (var script in AssetScripts) {
      Type componentType = Type.GetType(script)!;
      Component component = (Component)Activator.CreateInstance(componentType);
      entity.AddComponent(component);
    }
  }

  private void HandleComponents(Entity entity) {
    if (AssetType == typeof(Canvas).FullName) {
      entity.AddComponent(new Canvas());
    }

    if (AssetType == typeof(Terrain3D).FullName) {
      entity.AddMaterial();
      entity.AddComponent(new Terrain3D(Application.Instance));
      entity.GetComponent<Terrain3D>().Setup();
      entity.AddRigdbody(RigidbodyData!.Value.PrimitiveType, RigidbodyData!.Value.IsKinematic);
    }
  }

  private void HandleRigidbody(Entity entity) {
    if (RigidbodyData == null) return;
    var rbData = RigidbodyData!.Value;

    entity.AddRigdbody(rbData.PrimitiveType, rbData.Size, rbData.Offset, rbData.IsKinematic);
  }

  private void HandleCamera(Entity entity) {
    if (AssetType != typeof(Camera).FullName) return;

    var app = Application.Instance;
    var cameraData = CameraAsset!.Value;
    entity.AddComponent(new Camera(cameraData.Fov, app.Renderer.AspectRatio));
    entity.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
    entity.GetComponent<Camera>().Yaw = cameraData.Yaw;
    if (cameraData.LookAtTarget != Guid.Empty) {
      var target = app.GetEntity(cameraData.LookAtTarget);
      if (target != null) {
        // entity.AddComponent(new ThirdPersonCamera());
        entity.GetComponent<ThirdPersonCamera>().Init(target);
      }
    }
    CameraState.SetCamera(entity.GetComponent<Camera>());
    CameraState.SetCameraEntity(entity);
    CameraState.SetCameraSpeed(cameraData.CameraSpeed);
    app.SetCamera(entity);
  }
}
