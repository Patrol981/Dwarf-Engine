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

  public Rigidbody() { }

  public Rigidbody(Device device, PrimitiveType colliderShape) {
    _primitiveType = colliderShape;
    _device = device;
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
        mesh = Primitives.CreateCylinderPrimitive(0.25f, height, 20);
        shapeSettings = new CylinderShapeSettings(height / 2, 0.25f);
        break;
      case PrimitiveType.Convex:
        mesh = Primitives.CreateConvex(Owner!.GetComponent<Model>().Meshes[0]);
        shapeSettings = new BoxShapeSettings(new(height / 2, height / 2, height / 2));
        break;
      default:
        mesh = new();
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
