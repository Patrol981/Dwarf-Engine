using System.Numerics;

using Dwarf.Extensions.Logging;
using Dwarf.Loaders;
using Dwarf.Model;
using Dwarf.Model.Animation;
using Dwarf.Physics;
using Dwarf.Vulkan;

namespace Dwarf.EntityComponentSystem;
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

  public static async Task<Entity> Create3DModel(
    string entityName,
    string modelPath,
    Vector3? position = null,
    Vector3? rotation = null,
    Vector3? scale = null,
    bool sameTexture = false,
    int flip = 1
  ) {
    var app = Application.Instance;

    var entity = await CreateBase(entityName, position, rotation, scale);
    if (modelPath.Contains("glb")) {
      entity.AddComponent(await GLTFLoaderKHR.LoadGLTF(app, modelPath, flip));
      if (entity.GetComponent<MeshRenderer>().Animations.Count > 0) {
        entity.AddComponent(new AnimationController());
        entity.GetComponent<AnimationController>().Init(entity.GetComponent<MeshRenderer>());
      }
    } else {
      throw new Exception("Only .glb/gltf file are supported");
    }

    return entity;
  }

  public static async void AddModel(this Entity entity, string modelPath, int flip = 1) {
    var app = Application.Instance;

    if (!modelPath.Contains("glb")) {
      throw new Exception("This method does not support formats other than .glb");
    }

    Logger.Info($"{entity.Name} Mesh init");
    // entity.AddComponent(await GLTFLoader.LoadGLTF(app, modelPath, false, flip));
    entity.AddComponent(await GLTFLoaderKHR.LoadGLTF(app, modelPath, flip));
    if (entity.GetComponent<MeshRenderer>().Animations.Count > 0) {
      entity.AddComponent(new AnimationController());
      entity.GetComponent<AnimationController>().Init(entity.GetComponent<MeshRenderer>());
    }

    if (entity.GetComponent<MeshRenderer>().MeshedNodesCount < 1) {
      throw new Exception("Mesh is empty");
    }
  }

  public static async Task<Entity> Create3DPrimitive(
    string entityName,
    string texturePath,
    PrimitiveType primitiveType,
    Vector3? position = null,
    Vector3? rotation = null,
    Vector3? scale = null
  ) {
    var app = Application.Instance;

    var entity = await CreateBase(entityName, position, rotation, scale);
    app.Mutex.WaitOne();
    var mesh = Primitives.CreatePrimitive(primitiveType);
    var model = new MeshRenderer(app.Device, app.Renderer);
    Node node = new() { Mesh = mesh };
    node.Mesh.BindToTexture(app.TextureManager, texturePath);
    model.AddLinearNode(node);
    model.Init();
    entity.AddComponent(model);
    // entity.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, texturePath);
    app.Mutex.ReleaseMutex();

    return entity;
  }

  public static async void AddPrimitive(this Entity entity, string texturePath, PrimitiveType primitiveType = PrimitiveType.Cylinder) {
    var app = Application.Instance;

    app.Mutex.WaitOne();
    var mesh = Primitives.CreatePrimitive(primitiveType);
    var model = new MeshRenderer(app.Device, app.Renderer);
    Node node = new() { Mesh = mesh };
    node.Mesh.BindToTexture(app.TextureManager, texturePath);
    model.AddLinearNode(node);
    model.Init();
    entity.AddComponent(model);
    await app.TextureManager.AddTexture(texturePath);
    // entity.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, texturePath);
    app.Mutex.ReleaseMutex();
  }

  public static void AddRigdbody(
    VulkanDevice device,
    ref Entity entity,
    PrimitiveType primitiveType,
    float radius,
    bool kinematic = false,
    bool flip = false
  ) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(device, primitiveType, radius, kinematic, flip));
  }

  public static void AddRigdbody(
    VulkanDevice device,
    ref Entity entity,
    PrimitiveType primitiveType,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    bool kinematic = false,
    bool flip = false
  ) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(device, primitiveType, sizeX, sizeY, sizeZ, kinematic, flip));
  }

  public static void AddRigdbody(
    VulkanDevice device,
    ref Entity entity,
    PrimitiveType primitiveType,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    float offsetX = 0,
    float offsetY = 0,
    float offsetZ = 0,
    bool kinematic = false,
    bool flip = false
  ) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(device, primitiveType, sizeX, sizeY, sizeZ, offsetX, offsetY, offsetZ, kinematic, flip));
    entity.GetComponent<Rigidbody>().InitBase();
  }

  public static void AddRigdbody(this Entity entity, PrimitiveType primitiveType = PrimitiveType.Convex, bool kinematic = false, float radius = 1) {
    var device = Application.Instance.Device;

    AddRigdbody(device, ref entity, primitiveType, radius, kinematic);
  }

  public static void AddRigdbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    float offsetX = 0,
    float offsetY = 0,
    float offsetZ = 0,
    bool kinematic = false,
    bool flip = false
  ) {
    var device = Application.Instance.Device;
    AddRigdbody(device, ref entity, primitiveType, sizeX, sizeY, sizeZ, offsetX, offsetY, offsetZ, kinematic, flip);
  }

  public static void AddRigdbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    Vector3 size = default,
    Vector3 offset = default,
    bool kinematic = false,
    bool flip = false
  ) {
    var device = Application.Instance.Device;
    AddRigdbody(device, ref entity, primitiveType, size.X, size.Y, size.Z, offset.X, offset.Y, offset.Z, kinematic, flip);
  }

  public static void AddRigdbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    bool kinematic = false,
    bool flip = false
  ) {
    var device = Application.Instance.Device;
    AddRigdbody(device, ref entity, primitiveType, default, kinematic, flip);
  }
}
