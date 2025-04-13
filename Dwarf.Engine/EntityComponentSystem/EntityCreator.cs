using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Loaders;
using Dwarf.Physics;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Rendering.Renderer3D.Animations;
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
    entity.AddComponent(new MaterialComponent(new Vector3(1.0f, 1.0f, 1.0f)));

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

  public static void AddMaterial(this Entity entity, MaterialData materialData) {
    entity.AddComponent(new MaterialComponent(materialData));
  }

  public static void AddMaterial(this Entity entity, Vector3? color) {
    if (color == null) { color = Vector3.One; }
    entity.AddComponent(new MaterialComponent(color.Value));
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

  public static MeshRenderer CopyModel(in MeshRenderer copyRef) {
    var app = Application.Instance;
    var model = new MeshRenderer(app.Device, app.Renderer);
    copyRef.CopyTo(ref model);
    return model;
  }

  public static void AddSprite(this Entity entity, string spritePath, bool isSpriteSheet = false, int flip = 1) {
    var app = Application.Instance;

    entity.AddComponent(new Sprite(app, spritePath, isSpriteSheet, flip));
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
    await app.TextureManager.AddTextureLocal(texturePath);
    app.Mutex.ReleaseMutex();
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    PrimitiveType primitiveType,
    float radius,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(app.VmaAllocator, app.Device, primitiveType, radius, motionType, flip, useMesh: useMesh));
    entity.GetComponent<Rigidbody>().InitBase();
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    in Mesh mesh,
    PrimitiveType primitiveType,
    float radius,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(app.VmaAllocator, app.Device, primitiveType, radius, motionType, flip, useMesh: useMesh));
    entity.GetComponent<Rigidbody>().InitBase(mesh);
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    in Mesh mesh,
    PrimitiveType primitiveType,
    Vector3 size,
    Vector3 offset,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    entity.AddComponent(
      new Rigidbody(
        app.VmaAllocator,
        app.Device,
        primitiveType,
        motionType,
        size: size,
        offset: offset,
        flip,
        useMesh: useMesh
      )
    );
    entity.GetComponent<Rigidbody>().InitBase(mesh);
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    PrimitiveType primitiveType,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(app.VmaAllocator, app.Device, primitiveType, sizeX, sizeY, sizeZ, motionType, flip, useMesh: useMesh));
    entity.GetComponent<Rigidbody>().InitBase();
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    PrimitiveType primitiveType,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    float offsetX = 0,
    float offsetY = 0,
    float offsetZ = 0,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(app.VmaAllocator, app.Device, primitiveType, sizeX, sizeY, sizeZ, offsetX, offsetY, offsetZ, motionType, flip, useMesh: useMesh));
    entity.GetComponent<Rigidbody>().InitBase();
  }

  public static void AddRigidbody(this Entity entity, PrimitiveType primitiveType = PrimitiveType.Convex, MotionType motionType = MotionType.Dynamic, float radius = 1) {
    var app = Application.Instance;

    AddRigidbody(app, ref entity, primitiveType, radius, motionType);
  }

  public static void AddRigidbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    float offsetX = 0,
    float offsetY = 0,
    float offsetZ = 0,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, primitiveType, sizeX, sizeY, sizeZ, offsetX, offsetY, offsetZ, motionType, flip);
  }

  public static void AddRigidbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    Vector3 size = default,
    Vector3 offset = default,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, primitiveType, size.X, size.Y, size.Z, offset.X, offset.Y, offset.Z, motionType: motionType, flip: flip, useMesh: useMesh);
  }

  public static void AddRigidbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, primitiveType, default, motionType: motionType, flip: flip, useMesh: useMesh);
  }

  public static void AddRigidbody(
    this Entity entity,
    in Mesh mesh,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, mesh, primitiveType, default, motionType, flip);
  }

  public static void AddRigidbody(
    this Entity entity,
    in Mesh mesh,
    Vector3 size,
    Vector3 offset,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false
  ) {
    var app = Application.Instance;
    AddRigidbody(
      app,
      ref entity,
      mesh: in mesh,
      size: size,
      offset: offset,
      primitiveType: primitiveType,
      motionType: motionType,
      flip: flip
    );
  }
}
