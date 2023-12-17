using System.Numerics;

using Dwarf.Engine;
using Dwarf.Engine.Loader.Providers;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Physics;
using Dwarf.Vulkan;

namespace Dwarf.Engine.EntityComponentSystem;
public static class EntityCreator {

  /// <summary>
  /// Create Entity with commononly used components
  /// </summary>
  /// <returns>
  /// <c>Entity</c>
  /// </returns>
  public static Task<Entity> CreateBase(
    string entityName,
    Vector3? position = null,
    Vector3? rotation = null,
    Vector3? scale = null
  ) {
    var entity = new Entity();
    entity.Name = entityName;

    if (position == null) { position = Vector3.Zero; }
    if (rotation == null) { rotation = Vector3.Zero; }
    if (scale == null) { scale = Vector3.One; }

    entity.AddComponent(new Transform(position.Value));
    entity.GetComponent<Transform>().Rotation = rotation.Value;
    entity.GetComponent<Transform>().Scale = scale.Value;
    entity.AddComponent(new Material(new(1.0f, 1.0f, 1.0f)));

    return Task.FromResult(entity);
  }

  /// <summary>
  /// Adds <c>Transform</c> component to an <c>Entity</c>
  /// </summary>
  public static void AddTransform(this Entity entity) {
    entity.AddTransform(Vector3.Zero, Vector3.Zero, Vector3.One);
  }

  /// <summary>
  /// Adds <c>Transform</c> component to an <c>Entity</c>
  /// </summary>
  public static void AddTransform(this Entity entity, Vector3 position) {
    entity.AddTransform(position, Vector3.Zero, Vector3.One);
  }

  /// <summary>
  /// Adds <c>Transform</c> component to an <c>Entity</c>
  /// </summary>
  public static void AddTransform(this Entity entity, Vector3 position, Vector3 rotation) {
    entity.AddTransform(position, rotation, Vector3.One);
  }

  /// <summary>
  /// Adds <c>Transform</c> component to an <c>Entity</c>
  /// </summary>
  public static void AddTransform(this Entity entity, Vector3? position, Vector3? rotation, Vector3? scale) {
    if (position == null) { position = Vector3.Zero; }
    if (rotation == null) { rotation = Vector3.Zero; }
    if (scale == null) { scale = Vector3.One; }

    entity.AddComponent(new Transform(position.Value));
    entity.GetComponent<Transform>().Rotation = rotation.Value;
    entity.GetComponent<Transform>().Scale = scale.Value;
  }

  public static void AddMaterial(this Entity entity) {
    entity.AddMaterial(Vector3.One);
  }

  public static void AddMaterial(this Entity entity, Vector3? color) {
    if (color == null) { color = Vector3.One; }

    entity.AddComponent(new Material(color.Value));
  }

  public async static Task<Entity> Create3DModel(
    string entityName,
    string modelPath,
    string[] texturePaths,
    Vector3? position = null,
    Vector3? rotation = null,
    Vector3? scale = null,
    bool sameTexture = false,
    int flip = 1
  ) {
    var app = Application.Instance;

    var entity = await CreateBase(entityName, position, rotation, scale);
    if (modelPath.Contains("glb")) {
      var preload = texturePaths != null;

      entity.AddComponent(await GLTFLoader.Load(app, modelPath, preload, flip));

      if (entity.GetComponent<MeshRenderer>().MeshsesCount < 1) {
        throw new Exception("Mesh is empty");
      }

      if (texturePaths != null) {
        if (texturePaths.Length > 1) {
          entity.GetComponent<MeshRenderer>().BindMultipleModelPartsToTextures(app.TextureManager, texturePaths);
        } else {
          if (sameTexture) {
            entity.GetComponent<MeshRenderer>().BindMultipleModelPartsToTexture(app.TextureManager, texturePaths[0]);
          } else {
            entity.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, texturePaths[0]);
          }
        }
      }
    } else {
      entity.AddComponent(await new GenericLoader().LoadModelOptimized(app.Device, modelPath));

      if (texturePaths.Length > 1) {
        entity.GetComponent<MeshRenderer>().BindMultipleModelPartsToTextures(app.TextureManager, texturePaths);
      } else {
        if (sameTexture) {
          entity.GetComponent<MeshRenderer>().BindMultipleModelPartsToTexture(app.TextureManager, texturePaths[0]);
        } else {
          entity.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, texturePaths[0]);
        }
      }
    }

    return entity;
  }

  public static async void AddModel(this Entity entity, string modelPath, int flip = 1) {
    var app = Application.Instance;

    if (!modelPath.Contains("glb")) {
      throw new Exception("This method does not support formats other than .glb");
    }

    entity.AddComponent(await GLTFLoader.Load(app, modelPath, false, flip));

    if (entity.GetComponent<MeshRenderer>().MeshsesCount < 1) {
      throw new Exception("Mesh is empty");
    }
  }

  public async static Task<Entity> Create3DPrimitive(
    string entityName,
    string texturePath,
    PrimitiveType primitiveType,
    Vector3? position = null,
    Vector3? rotation = null,
    Vector3? scale = null
  ) {
    var app = Application.Instance;

    var entity = await CreateBase(entityName, position, rotation, scale);
    var mesh = Primitives.CreatePrimitive(primitiveType);
    var model = new MeshRenderer(app.Device, new[] { mesh });
    entity.AddComponent(model);

    entity.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, texturePath);

    return entity;
  }

  public static void AddPrimitive(this Entity entity, string texturePath, PrimitiveType primitiveType = PrimitiveType.Cylinder) {
    var app = Application.Instance;

    var mesh = Primitives.CreatePrimitive(primitiveType);
    var model = new MeshRenderer(app.Device, new[] { mesh });
    entity.AddComponent(model);
    entity.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, texturePath);
  }

  public static void AddRigdbody(Device device, ref Entity entity, PrimitiveType primitiveType, float radius, bool kinematic = false) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(device, primitiveType, radius, kinematic));
  }

  public static void AddRigdbody(this Entity entity, PrimitiveType primitiveType = PrimitiveType.Convex, bool kinematic = false, float radius = 1) {
    var device = Application.Instance.Device;

    AddRigdbody(device, ref entity, primitiveType, radius, kinematic);
  }
}
