using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Vulkan;

using JoltPhysicsSharp;

using static Dwarf.Physics.JoltConfig;

namespace Dwarf.Physics;

public class Rigidbody : Component, IDisposable {
  private readonly VulkanDevice _device = null!;
  private BodyInterface _bodyInterface;

  private BodyID _bodyId;
  private MotionType _motionType = MotionType.Dynamic;
  private readonly MotionQuality _motionQuality = MotionQuality.LinearCast;
  private readonly bool _physicsControlRotation = false;
  private readonly float _inputRadius = 0.0f;
  private readonly float _sizeX = 1.0f;
  private readonly float _sizeY = 1.0f;
  private readonly float _sizeZ = 1.0f;
  private readonly float _offsetX = 0.0f;
  private readonly float _offsetY = 0.0f;
  private readonly float _offsetZ = 0.0f;

  public Rigidbody() { }

  public Rigidbody(
    VulkanDevice device,
    PrimitiveType colliderShape,
    float inputRadius,
    bool kinematic = false,
    bool flip = false,
    bool physicsControlRotation = false
  ) {
    PrimitiveType = colliderShape;
    _device = device;
    _inputRadius = inputRadius;
    Flipped = flip;
    if (kinematic) {
      _motionType = MotionType.Kinematic;
    }
    _physicsControlRotation = physicsControlRotation;
  }

  public Rigidbody(
    VulkanDevice device,
    PrimitiveType primitiveType,
    float sizeX,
    float sizeY,
    float sizeZ,
    bool kinematic,
    bool flip,
    bool physicsControlRotation = false
  ) {
    _device = device;
    PrimitiveType = primitiveType;
    Flipped = flip;
    if (kinematic) {
      _motionType = MotionType.Kinematic;
    }
    _sizeX = sizeX;
    _sizeY = sizeY;
    _sizeZ = sizeZ;
    _physicsControlRotation = physicsControlRotation;
  }

  public Rigidbody(
    VulkanDevice device,
    PrimitiveType primitiveType,
    float sizeX,
    float sizeY,
    float sizeZ,
    float offsetX,
    float offsetY,
    float offsetZ,
    bool kinematic,
    bool flip,
    bool physicsControlRotation = false
  ) {
    _device = device;
    PrimitiveType = primitiveType;
    Flipped = flip;
    if (kinematic) {
      _motionType = MotionType.Kinematic;
    }
    _sizeX = sizeX;
    _sizeY = sizeY;
    _sizeZ = sizeZ;
    _offsetX = offsetX;
    _offsetY = offsetY;
    _offsetZ = offsetZ;
    _physicsControlRotation = physicsControlRotation;
  }

  public void InitBase() {
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_device == null) throw new Exception("Device cannot be null!");

    if (Owner?.GetDrawable<IRender3DElement>() == null) return;
    var target = Owner!.GetDrawable<IRender3DElement>() as IRender3DElement;

    Mesh mesh;

    switch (PrimitiveType) {
      case PrimitiveType.Cylinder:
        mesh = Primitives.CreateCylinderPrimitive(1, 1, 20);
        ScaleColliderMesh(mesh);
        AdjustColliderMesh(mesh);
        break;
      case PrimitiveType.Convex:
        mesh = Primitives.CreateConvex(target!.MeshedNodes, Flipped);
        ScaleColliderMesh(mesh);
        AdjustColliderMesh(mesh);
        break;
      case PrimitiveType.Box:
        mesh = Primitives.CreateBoxPrimitive(1);
        ScaleColliderMesh(mesh);
        AdjustColliderMesh(mesh);
        break;
      default:
        mesh = Primitives.CreateBoxPrimitive(1);
        break;
    }

    Owner!.AddComponent(new ColliderMesh(_device, mesh));
  }

  public unsafe void Init(in BodyInterface bodyInterface) {
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_device == null) throw new Exception("Device cannot be null!");

    _bodyInterface = bodyInterface;

    var pos = Owner!.GetComponent<Transform>().Position;
    Mesh mesh = Owner!.GetComponent<ColliderMesh>().Mesh;
    ShapeSettings shapeSettings;

    switch (PrimitiveType) {
      case PrimitiveType.Cylinder:
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Convex:
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Box:
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      default:
        shapeSettings = new BoxShapeSettings(new(1 / 2, 1 / 2, 1 / 2));
        break;
    }

    BodyCreationSettings settings = new(
        shapeSettings,
        pos,
        Quaternion.Identity,
        _motionType,
        Layers.Moving
      );
    _bodyId = _bodyInterface.CreateAndAddBody(settings, Activation.Activate);

    _bodyInterface.SetGravityFactor(_bodyId, 0.1f);
    _bodyInterface.SetMotionQuality(_bodyId, _motionQuality);
    _bodyInterface.SetMotionType(_bodyId, _motionType, Activation.Activate);
  }

  private void AdjustColliderMesh(Mesh colliderMesh) {
    for (int i = 0; i < colliderMesh.Vertices.Length; i++) {
      colliderMesh.Vertices[i].Position.X += _offsetX;
      colliderMesh.Vertices[i].Position.Y += _offsetY;
      colliderMesh.Vertices[i].Position.Z += _offsetZ;
    }
  }

  private void ScaleColliderMesh(Mesh colliderMesh) {
    for (int i = 0; i < colliderMesh.Vertices.Length; i++) {
      colliderMesh.Vertices[i].Position.X *= _sizeX;
      colliderMesh.Vertices[i].Position.Y *= _sizeY;
      colliderMesh.Vertices[i].Position.Z *= _sizeZ;
    }
  }

  private ConvexHullShapeSettings ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    List<Vector3> vertices = [];
    var scale = entity.GetComponent<Transform>().Scale;
    foreach (var m in colliderMesh.Vertices) {
      Vertex v = new();
      v.Position.X = m.Position.X * scale.X;
      v.Position.Y = m.Position.Y * scale.Y;
      v.Position.Z = m.Position.Z * scale.Z;
      vertices.Add(v.Position);
    }

    ConvexHullShapeSettings settings = new(vertices.ToArray(), 0.01f);
    return settings;
  }

  public void Update() {
    var pos = _bodyInterface.GetPosition(_bodyId);
    var transform = Owner!.GetComponent<Transform>();

    transform.Position = pos;

    if (!_physicsControlRotation) {
      var quat = Quaternion.CreateFromRotationMatrix(transform.AngleYMatrix);
      _bodyInterface.SetRotation(_bodyId, quat, Activation.Activate);
    } else {
      transform.Rotation = Quat.ToEuler(_bodyInterface.GetRotation(_bodyId));
    }

    if (_motionType == MotionType.Dynamic) {
      var newVel = _bodyInterface.GetLinearVelocity(_bodyId);
      newVel.X /= 2;
      newVel.Z /= 2;
      if (newVel.Y < 0) newVel.Y = 0;
      _bodyInterface.SetLinearVelocity(_bodyId, newVel);
    } else {
      var newVel = _bodyInterface.GetLinearVelocity(_bodyId);
      newVel.X /= 2;
      newVel.Z /= 2;
      newVel.Y = transform.Position.Y;
      _bodyInterface.SetLinearVelocity(_bodyId, newVel);
    }

    // freeze rigidbody to X an Z axis
    // _bodyInterface.SetRotation(_bodyId, new System.Numerics.Quaternion(0.0f, rot.Y, 0.0f, 1.0f), Activation.Activate);
  }

  public void AddForce(Vector3 vec3) {
    _bodyInterface.AddForce(_bodyId, vec3);
  }

  public void AddVelocity(Vector3 vec3) {
    _bodyInterface.AddLinearVelocity(_bodyId, vec3);
  }

  public void AddImpulse(Vector3 vec3) {
    _bodyInterface.AddImpulse(_bodyId, vec3);
  }

  public void Translate(Vector3 vec3) {
    var pos = _bodyInterface.GetPosition(_bodyId);
    pos.X += vec3.X;
    pos.Y += vec3.Y;
    pos.Z += vec3.Z;
    // _bodyInterface.SetPosition(_bodyId, pos, Activation.Activate);
    _bodyInterface.AddLinearVelocity(_bodyId, vec3);
  }

  public void Rotate(Vector3 vec3) {
    var rot = _bodyInterface.GetRotation(_bodyId);
    rot.X += vec3.X;
    rot.Y += vec3.Y;
    rot.Z += vec3.Z;
    _bodyInterface.SetRotation(_bodyId, rot, Activation.Activate);
  }

  public void SetRotation(Vector3 vec3) {
    var rot = _bodyInterface.GetRotation(_bodyId);
    rot.X = vec3.X;
    rot.Y = vec3.Y;
    rot.Z = vec3.Z;
    _bodyInterface.SetRotation(_bodyId, rot, Activation.Activate);
  }

  public void SetPosition(Vector3 vec3) {
    _bodyInterface.SetPosition(_bodyId, new(vec3.X, vec3.Y, vec3.Z), Activation.Activate);
  }

  public Vector3 Velocity {
    get {
      return _bodyInterface.GetLinearVelocity(_bodyId);
    }
  }

  public static (Entity?, Entity?) GetCollisionData(BodyID body1, BodyID body2) {
    var entities = Application.Instance.GetEntities().Where(x => x.HasComponent<Rigidbody>() && !x.CanBeDisposed);
    var first = entities.Where(x => x.GetComponent<Rigidbody>()._bodyId == body1).FirstOrDefault();
    var second = entities.Where(x => x.GetComponent<Rigidbody>()._bodyId == body2).FirstOrDefault();

    return (first, second);
  }

  public void InvokeCollision(CollisionState collisionState, Entity otherColl) {
    var scripts = Owner!.GetScripts();
    for (short i = 0; i < scripts.Length; i++) {
      switch (collisionState) {
        case CollisionState.Enter:
          scripts[i].CollisionEnter(otherColl);
          break;
        case CollisionState.Stay:
          scripts[i].CollisionStay(otherColl);
          break;
        case CollisionState.Exit:
          scripts[i].CollisionExit(otherColl);
          break;
        default:
          break;
      }
    }
  }


  public bool Kinematic {
    get {
      return _motionType == MotionType.Static;
    }
    set {
      _motionType = value ? MotionType.Static : MotionType.Dynamic;
    }
  }

  public MotionType MotionType => _motionType;

  public Vector3 Offset => new(_offsetX, _offsetY, _offsetZ);
  public Vector3 Size => new(_sizeX, _sizeY, _sizeZ);
  public Quaternion Rotation => _bodyInterface.GetRotation(_bodyId);
  public bool Flipped { get; } = false;

  public PrimitiveType PrimitiveType { get; } = PrimitiveType.None;

  public void Dispose() {
    if (!_bodyInterface.IsNull) {
      _bodyInterface.DeactivateBody(_bodyId);
      _bodyInterface.RemoveBody(_bodyId);
      _bodyInterface.DestroyBody(_bodyId);
    }
  }
}
