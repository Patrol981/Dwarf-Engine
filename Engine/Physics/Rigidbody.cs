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

using OpenTK.Mathematics;

using static Dwarf.Engine.Physics.JoltConfig;

namespace Dwarf.Engine.Physics;
public class Rigidbody : Component, IDisposable {
  private readonly Device _device = null!;
  private BodyInterface _bodyInterface;

  private BodyID _bodyId;
  private MotionType _motionType = MotionType.Dynamic;
  private MotionQuality _motionQuality = MotionQuality.LinearCast;
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

    var pos = Translator.OpenTKToSystemNumericsVector(Owner!.GetComponent<Transform>().Position);

    var height = Owner!.GetComponent<Model>().CalculateHeightOfAnModel();
    Mesh mesh;
    ShapeSettings shapeSettings;

    switch (_primitiveType) {
      case PrimitiveType.Cylinder:
        mesh = Primitives.CreateCylinderPrimitive(0.25f, height, 20);
        shapeSettings = new CylinderShapeSettings(height / 2, 0.25f);
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

    var pos = _bodyInterface.GetCenterOfMassPosition(_bodyId);
    var vec3 = new OpenTK.Mathematics.Vector3((float)pos.X, (float)pos.Y, (float)pos.Z);
    Owner!.GetComponent<Transform>().Position = vec3;
    // bodyInterface.MoveKinematic(_bodyId, )
    // Owner.GetComponent<Transform>().Position = _bodyId.
    // Logger.Info($"[ACTIVE] {_bodyInterface.IsActive(_bodyId)}");
    // _bodyInterface.ActivateBody(_bodyId);
  }

  public void AddForce(OpenTK.Mathematics.Vector3 vec3) {
    _bodyInterface.AddForce(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    _bodyInterface.ActivateBody(_bodyId);
  }

  public void AddVelocity(OpenTK.Mathematics.Vector3 vec3) {
    _bodyInterface.AddLinearVelocity(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    _bodyInterface.ActivateBody(_bodyId);
  }

  public void AddImpulse(OpenTK.Mathematics.Vector3 vec3) {
    _bodyInterface.AddImpulse(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    _bodyInterface.ActivateBody(_bodyId);
  }

  public void Translate(OpenTK.Mathematics.Vector3 vec3) {
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
    _bodyInterface.DestroyBody(_bodyId);
  }
}
