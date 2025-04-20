using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Hammer;
using Dwarf.Hammer.Models;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D.Components;
using Dwarf.Rendering.Renderer2D.Helpers;

namespace Dwarf.Physics.Backends.Hammer;

public class HammerBodyWrapper : IPhysicsBody2D {
  private readonly HammerInterface _hammerInterface;
  private BodyId _bodyId = null!;

  public HammerBodyWrapper(in HammerInterface hammerInterface) {
    _hammerInterface = hammerInterface;
  }

  public object BodyId => _bodyId;

  public Vector2 Position {
    get => _hammerInterface.GetPosition(_bodyId);
    set => _hammerInterface.SetPosition(_bodyId, value);
  }
  public Vector2 LinearVelocity {
    get => _hammerInterface.GetVelocity(_bodyId);
    set => _hammerInterface.SetVelocity(_bodyId, value);
  }
  public Vector2 AngularVelocity {
    get => _hammerInterface.GetVelocity(_bodyId);
    set => _hammerInterface.SetVelocity(_bodyId, value);
  }
  public float GravityFactor {
    get => _hammerInterface.GetGravity();
    set => _hammerInterface.SetGravity(value);
  }
  public MotionQuality MotionQuality {
    get => (MotionQuality)_hammerInterface.GetMotionQuality(_bodyId);
    set => _hammerInterface.SetMotionQuality(_bodyId, (Dwarf.Hammer.Enums.MotionQuality)value);
  }
  public MotionType MotionType {
    get => (MotionType)_hammerInterface.GetMotionType(_bodyId);
    set => _hammerInterface.SetMotionType(_bodyId, (Dwarf.Hammer.Enums.MotionType)value);
  }

  public object CreateAndAddBody(object settings) {
    _bodyId = _hammerInterface.CreateAndAddBody((ShapeSettings)settings, Dwarf.Hammer.Enums.MotionType.Dynamic, Vector2.Zero);

    return null!;
  }

  public object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    var transform = entity.GetComponent<Transform>();
    List<Dwarf.Hammer.Structs.Vertex> vertices = [];
    foreach (var m in colliderMesh.Vertices) {
      Dwarf.Hammer.Structs.Vertex v = new() {
        X = m.Position.X,
        Y = m.Position.Y
      };

      vertices.Add(v);
    }

    var rigidbody = entity.GetComponent<Rigidbody2D>();

    object userData;
    Dwarf.Hammer.Enums.ObjectType objectType = Dwarf.Hammer.Enums.ObjectType.Sprite;
    var tilemap = entity.TryGetComponent<Tilemap>();
    if (tilemap != null) {
      // var edges = tilemap.ExtractEgdges();
      // var hammerEdges = new List<Dwarf.Hammer.Structs.Edge>();
      // foreach (var edge in edges) {
      //   hammerEdges.Add(new() {
      //     A = edge.A,
      //     B = edge.B,
      //     Normal = edge.Normal,
      //   });
      // }

      var aabbs = tilemap.ExtractAABBs();

      userData = aabbs;
      objectType = Dwarf.Hammer.Enums.ObjectType.Tilemap;
    } else {
      userData = (rigidbody.Min, rigidbody.Max);
    }

    ShapeSettings shapeSettings = new ShapeSettings(
      new Dwarf.Hammer.Structs.Mesh() {
        Vertices = [.. vertices],
        Indices = colliderMesh.Indices
      },
      userData,
      objectType
    );

    return shapeSettings;
  }

  public void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector2 position) {
    _bodyId = _hammerInterface.CreateAndAddBody(
      (ShapeSettings)shapeSettings,
      (Dwarf.Hammer.Enums.MotionType)motionType,
      position
    );
  }

  public void SetActive(bool value) {
    throw new NotImplementedException();
  }

  public void AddForce(Vector2 force) {
    _hammerInterface.AddForce(_bodyId, force);
  }

  public void AddLinearVelocity(Vector2 velocity) {
    _hammerInterface.AddVelocity(_bodyId, velocity);
  }

  public void AddImpulse(Vector2 impulse) {
    throw new NotImplementedException();
  }

  public void Dispose() {
    throw new NotImplementedException();
  }
}