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
  private IPhysicsBody _bodyInterface;

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
    MotionType motionType,
    bool flip = false,
    bool physicsControlRotation = false
  ) {
    PrimitiveType = colliderShape;
    _device = device;
    _inputRadius = inputRadius;
    Flipped = flip;
    _motionType = motionType;
    _physicsControlRotation = physicsControlRotation;
  }

  public Rigidbody(
    VulkanDevice device,
    PrimitiveType primitiveType,
    float sizeX,
    float sizeY,
    float sizeZ,
    MotionType motionType,
    bool flip,
    bool physicsControlRotation = false
  ) {
    _device = device;
    PrimitiveType = primitiveType;
    Flipped = flip;
    _motionType = motionType;
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
    MotionType motionType,
    bool flip,
    bool physicsControlRotation = false
  ) {
    _device = device;
    PrimitiveType = primitiveType;
    Flipped = flip;
    _motionType = motionType;
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

  public unsafe void Init(in IPhysicsBody bodyInterface) {
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_device == null) throw new Exception("Device cannot be null!");

    _bodyInterface = bodyInterface;

    var pos = Owner!.GetComponent<Transform>().Position;
    Mesh mesh = Owner!.GetComponent<ColliderMesh>().Mesh;
    object shapeSettings;

    switch (PrimitiveType) {
      case PrimitiveType.Cylinder:
        shapeSettings = _bodyInterface.ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Convex:
        shapeSettings = _bodyInterface.ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Box:
        shapeSettings = _bodyInterface.ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      default:
        // shapeSettings = new BoxShapeSettings(new(1 / 2, 1 / 2, 1 / 2));
        throw new NotImplementedException();
    }

    _bodyInterface.CreateAndAddBody(_motionType, shapeSettings, pos);

    _bodyInterface.GravityFactor = 0.1f;
    _bodyInterface.MotionQuality = _motionQuality;
    _bodyInterface.MotionType = _motionType;
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

  public void Update() {
    var pos = _bodyInterface.Position;
    var transform = Owner!.GetComponent<Transform>();

    transform.Position = pos;

    if (!_physicsControlRotation) {
      var quat = Quaternion.CreateFromRotationMatrix(transform.AngleYMatrix);
      _bodyInterface.Rotation = quat;
    } else {
      transform.Rotation = Quat.ToEuler(_bodyInterface.Rotation);
    }

    if (_motionType == MotionType.Dynamic) {
      var newVel = _bodyInterface.LinearVelocity;
      newVel.X /= 2;
      newVel.Z /= 2;
      if (newVel.Y < 0) newVel.Y = 0;
      _bodyInterface.LinearVelocity = newVel;
    } else {
      var newVel = _bodyInterface.LinearVelocity;
      newVel.X /= 2;
      newVel.Z /= 2;
      newVel.Y = transform.Position.Y;
      _bodyInterface.LinearVelocity = newVel;
    }

    // freeze rigidbody to X an Z axis
    // _bodyInterface.SetRotation(_bodyId, new System.Numerics.Quaternion(0.0f, rot.Y, 0.0f, 1.0f), Activation.Activate);
  }

  public void AddForce(Vector3 vec3) {
    _bodyInterface.AddForce(vec3);
  }

  public void AddVelocity(Vector3 vec3) {
    _bodyInterface.AddLinearVelocity(vec3);
  }

  public void AddImpulse(Vector3 vec3) {
    _bodyInterface.AddImpulse(vec3);
  }

  public void Translate(Vector3 vec3) {
    _bodyInterface.AddLinearVelocity(vec3);
  }

  public void Rotate(Vector3 vec3) {
    var rot = _bodyInterface.Rotation;
    rot.X += vec3.X;
    rot.Y += vec3.Y;
    rot.Z += vec3.Z;
    _bodyInterface.Rotation = rot;
  }

  public void SetRotation(Vector3 vec3) {
    var rot = _bodyInterface.Rotation;
    _bodyInterface.Rotation = new(vec3, rot.Z);
  }

  public void SetPosition(Vector3 vec3) {
    _bodyInterface.Position = vec3;
  }

  public Vector3 Velocity {
    get {
      return _bodyInterface.LinearVelocity;
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
  public Quaternion Rotation => _bodyInterface.Rotation;
  public bool Flipped { get; } = false;

  public PrimitiveType PrimitiveType { get; } = PrimitiveType.None;

  public void Dispose() {
    GC.SuppressFinalize(this);
  }
}
