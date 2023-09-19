using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Math;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using JoltPhysicsSharp;

using System.Numerics;

using static Dwarf.Engine.Physics.JoltConfig;

namespace Dwarf.Engine.Physics;
public class Rigidbody : Component, IDisposable {
  private readonly Device _device = null!;
  private BodyInterface _bodyInterface;

  private BodyID _bodyId;
  private MotionType _motionType = MotionType.Dynamic;
  private MotionQuality _motionQuality = MotionQuality.Discrete;
  private PrimitiveType _primitiveType = PrimitiveType.None;
  private float _inputRadius = 0.0f;

  public Rigidbody() { }

  public Rigidbody(Device device, PrimitiveType colliderShape, float inputRadius) {
    _primitiveType = colliderShape;
    _device = device;
    _inputRadius = inputRadius;
  }

  public unsafe void Init(in BodyInterface bodyInterface) {
    if (_primitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_device == null) throw new Exception("Device cannot be null!");

    _bodyInterface = bodyInterface;

    // var pos = Translator.OpenTKToSystemNumericsVector(Owner!.GetComponent<Transform>().Position);
    var pos = Owner!.GetComponent<Transform>().Position;

    var height = Owner!.GetComponent<Model>().CalculateHeightOfAnModel();
    Mesh mesh;
    ShapeSettings shapeSettings;

    switch (_primitiveType) {
      case PrimitiveType.Cylinder:
        mesh = Primitives.CreateCylinderPrimitive(_inputRadius, height, 20);
        // shapeSettings = new CylinderShapeSettings(height / 2, 0.25f);
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Convex:
        mesh = Primitives.CreateConvex(Owner!.GetComponent<Model>().Meshes[0]);
        shapeSettings = ColldierMeshToPhysicsShape(Owner, mesh);
        break;
      case PrimitiveType.Box:
        mesh = Primitives.CreateBoxPrimitive(_inputRadius, height);
        // shapeSettings = new BoxShapeSettings(new(height / 2, height / 2, height / 2));
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
        System.Numerics.Quaternion.Identity,
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
    // var pos = _bodyInterface.GetCenterOfMassPosition(_bodyId);
    var pos = _bodyInterface.GetPosition(_bodyId);
    var rot = _bodyInterface.GetRotation(_bodyId);
    Owner!.GetComponent<Transform>().Position = pos;

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

  public void GetMeshData() {

  }

  public void Dispose() {
    _bodyInterface.DeactivateBody(_bodyId);
    _bodyInterface.RemoveBody(_bodyId);
    _bodyInterface.DestroyBody(_bodyId);
  }
}
