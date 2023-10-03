using System.Numerics;

using Dwarf.Engine;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Physics;
using Dwarf.Vulkan;

namespace Dwarf.Engine.EntityComponentSystem;
public static class EntityCreator {

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

  public async static Task<Entity> Create3DModel(
    string entityName,
    string modelPath,
    string[] texturePaths,
    Vector3? position = null,
    Vector3? rotation = null,
    Vector3? scale = null
  ) {
    var app = ApplicationState.Instance;

    var entity = await CreateBase(entityName, position, rotation, scale);
    entity.AddComponent(await new GenericLoader().LoadModelOptimized(app.Device, modelPath));

    if (texturePaths.Length > 1) {
      entity.GetComponent<Model>().BindMultipleModelPartsToTextures(app.TextureManager, texturePaths);
    } else {
      entity.GetComponent<Model>().BindToTexture(app.TextureManager, texturePaths[0]);
    }

    return entity;
  }

  public async static Task<Entity> Create3DPrimitive(
    string entityName,
    string texturePath,
    PrimitiveType primitiveType,
    Vector3? position = null,
    Vector3? rotation = null,
    Vector3? scale = null
  ) {
    var app = ApplicationState.Instance;

    var entity = await CreateBase(entityName, position, rotation, scale);
    var mesh = Primitives.CreatePrimitive(primitiveType);
    var model = new Model(app.Device, new[] { mesh });
    entity.AddComponent(model);

    entity.GetComponent<Model>().BindToTexture(app.TextureManager, texturePath);

    return entity;
  }

  public static void AddRigdbody(Device device, ref Entity entity, PrimitiveType primitiveType, float radius) {
    if (entity == null) return;

    entity.AddComponent(new Rigidbody(device, primitiveType, radius));
  }
}
