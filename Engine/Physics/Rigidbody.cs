using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Vulkan;

using JoltPhysicsSharp;

using System.Numerics;

using static Dwarf.Engine.Physics.JoltConfig;
using Dwarf.Engine.Rendering;

namespace Dwarf.Engine.Physics;

public class Rigidbody : Component, IDisposable {
  private readonly Device _device = null!;
  private BodyInterface _bodyInterface;

  private BodyID _bodyId;
  private MotionType _motionType = MotionType.Dynamic;
  private MotionQuality _motionQuality = MotionQuality.LinearCast;
  private PrimitiveType _primitiveType = PrimitiveType.None;
  private float _inputRadius = 0.0f;
  private bool _flip = false;

  public Rigidbody() { }

  public Rigidbody(Device device, PrimitiveType colliderShape, float inputRadius, bool kinematic = false, bool flip = false) {
    _primitiveType = colliderShape;
    _device = device;
    _inputRadius = inputRadius;
    _flip = flip;
    if (kinematic) {
      _motionType = MotionType.Kinematic;
    }
  }

  public unsafe void Init(in BodyInterface bodyInterface) {
    if (_primitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_device == null) throw new Exception("Device cannot be null!");

    _bodyInterface = bodyInterface;

    var pos = Owner!.GetComponent<Transform>().Position;

    var target = Owner.GetDrawable<IRender3DElement>() as IRender3DElement;
    var height = target!.CalculateHeightOfAnModel();
    Mesh mesh;
    ShapeSettings shapeSettings;

    switch (_primitiveType) {
      case PrimitiveType.Cylinder:
        mesh = Primitives.CreateCylinderPrimitive(_inputRadius, height, 20);
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Convex:
        mesh = Primitives.CreateConvex(target.Meshes, _flip);
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Box:
        mesh = Primitives.CreateBoxPrimitive(_inputRadius, height);
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      default:
        mesh = Primitives.CreateBoxPrimitive(0.25f, height);
        shapeSettings = new BoxShapeSettings(new(height / 2, height / 2, height / 2));
        break;
    }

    Owner!.AddComponent(new ColliderMesh(_device, mesh));

    BodyCreationSettings settings = new(
        shapeSettings,
        pos,
        Quaternion.Identity,
        _motionType,
        Layers.Moving
      );
    _bodyId = _bodyInterface.CreateAndAddBody(settings, Activation.Activate);

    _bodyInterface.SetGravityFactor(_bodyId, 0.025f);
    _bodyInterface.SetMotionQuality(_bodyId, _motionQuality);
  }

  private ConvexHullShapeSettings ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    List<Vector3> vertices = new();
    var scale = entity.GetComponent<Transform>().Scale;
    foreach (var m in colliderMesh.Vertices) {
      Vertex v = new();
      v.Position.X = m.Position.X * scale.X;
      v.Position.Y = m.Position.Y * scale.Y;
      v.Position.Z = m.Position.Z * scale.Z;
      vertices.Add(v.Position);
    }

    ConvexHullShapeSettings settings = new(vertices.ToArray());
    return settings;
  }

  public void Update() {
    var pos = _bodyInterface.GetPosition(_bodyId);
    var rot = _bodyInterface.GetRotation(_bodyId);
    var transform = Owner!.GetComponent<Transform>();

    transform.Position = pos;

    if (_bodyInterface.GetMotionType(_bodyId) != _motionType) {
      _bodyInterface.SetMotionType(_bodyId, _motionType, Activation.Activate);
    }

    // freeze rigidbody to X an Z axis
    _bodyInterface.SetRotation(_bodyId, new System.Numerics.Quaternion(0.0f, rot.Y, 0.0f, 1.0f), Activation.Activate);
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
    _bodyInterface.SetPosition(_bodyId, pos, Activation.Activate);
  }

  public void Rotate(Vector3 vec3) {
    var rot = _bodyInterface.GetRotation(_bodyId);
    rot.X += vec3.X;
    rot.Y += vec3.Y;
    rot.Z += vec3.Z;
    _bodyInterface.SetRotation(_bodyId, rot, Activation.Activate);
  }

  public void SetPosition(Vector3 vec3) {
    _bodyInterface.SetPosition(_bodyId, new(vec3.X, vec3.Y, vec3.Z), Activation.Activate);
  }

  public static (Entity?, Entity?) GetCollisionData(BodyID body1, BodyID body2) {
    var entities = Application.Instance.GetEntities().Where(x => x.HasComponent<Rigidbody>());
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
      if (_motionType == MotionType.Static) return true;
      return false;
    }
    set {
      if (value) {
        _motionType = MotionType.Static;
      } else {
        _motionType = MotionType.Dynamic;
      }
    }
  }

  public void Dispose() {
    _bodyInterface.DeactivateBody(_bodyId);
    _bodyInterface.RemoveBody(_bodyId);
    _bodyInterface.DestroyBody(_bodyId);
  }
}
