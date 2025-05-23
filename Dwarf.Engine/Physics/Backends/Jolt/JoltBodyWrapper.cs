using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;
using JoltPhysicsSharp;
using static Dwarf.Physics.JoltConfig;

namespace Dwarf.Physics.Backends.Jolt;

public class JoltBodyWrapper : IPhysicsBody {
  private readonly BodyInterface _bodyInterface;
  private BodyID _bodyID;
  private Activation _activation = Activation.Activate;

  public JoltBodyWrapper(in BodyInterface bodyInterface) {
    _bodyInterface = bodyInterface;
  }

  public Quaternion Rotation {
    get => _bodyInterface.GetRotation(_bodyID);
    set => _bodyInterface.SetRotation(_bodyID, value, _activation);
  }

  public Vector3 Position {
    get => _bodyInterface.GetPosition(_bodyID);
    set => _bodyInterface.SetPosition(_bodyID, value, _activation);
  }

  public Vector3 LinearVelocity {
    get => _bodyInterface.GetLinearVelocity(_bodyID);
    set => _bodyInterface.SetLinearVelocity(_bodyID, value);
  }

  public Vector3 AngularVelocity {
    get => _bodyInterface.GetAngularVelocity(_bodyID);
    set => _bodyInterface.SetAngularVelocity(_bodyID, value);
  }

  public float GravityFactor {
    get => _bodyInterface.GetGravityFactor(_bodyID);
    set => _bodyInterface.SetGravityFactor(_bodyID, value);
  }

  public Dwarf.Physics.MotionQuality MotionQuality {
    get => (Dwarf.Physics.MotionQuality)_bodyInterface.GetMotionQuality(_bodyID);
    set => _bodyInterface.SetMotionQuality(_bodyID, (JoltPhysicsSharp.MotionQuality)value);
  }

  public Dwarf.Physics.MotionType MotionType {
    get => (Dwarf.Physics.MotionType)_bodyInterface.GetMotionType(_bodyID);
    set => _bodyInterface.SetMotionType(_bodyID, (JoltPhysicsSharp.MotionType)value, _activation);
  }

  public object BodyId => _bodyID;

  public object CreateAndAddBody(object settings) {
    return _bodyInterface.CreateAndAddBody((BodyCreationSettings)settings, _activation);
  }

  public void SetActive(bool value) {
    if (value) {
      _bodyInterface.ActivateBody(_bodyID);
      _activation = Activation.Activate;
      _bodyInterface.AddBody(_bodyID, _activation);
    } else {
      _bodyInterface.DeactivateBody(_bodyID);
      _activation = Activation.DontActivate;
      _bodyInterface.RemoveBody(_bodyID);
    }
  }

  public object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    List<Vector3> vertices = [];
    var scale = entity.GetComponent<Transform>().Scale;
    foreach (var m in colliderMesh.Vertices) {
      Vertex v = new();
      v.Position.X = m.Position.X * scale.X;
      v.Position.Y = m.Position.Y * scale.Y;
      v.Position.Z = m.Position.Z * scale.Z;
      vertices.Add(v.Position);
    }

    ConvexHullShapeSettings settings = new([.. vertices]);
    // ConvexHullShapeSettings settings = new ()
    // BoxShapeSettings settings = new(new(1, 1, 1));
    return settings;
  }

  public void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector3 position) {
    var settings = new BodyCreationSettings(
      (ShapeSettings)shapeSettings,
      position,
      Quaternion.Identity,
      (JoltPhysicsSharp.MotionType)motionType,
      Layers.Moving
    );

    _bodyID = _bodyInterface.CreateAndAddBody(settings, _activation);
  }

  public void AddForce(Vector3 force) {
    _bodyInterface.AddForce(_bodyID, force);
  }

  public void AddLinearVelocity(Vector3 velocity) {
    _bodyInterface.AddLinearVelocity(_bodyID, velocity);
  }

  public void AddImpulse(Vector3 impulse) {
    _bodyInterface.AddImpulse(_bodyID, impulse);
  }

  public static (Entity?, Entity?) GetCollisionData(BodyID body1, BodyID body2) {
    var entities = Application.Instance.GetEntities().Where(x => !x.CanBeDisposed && x.HasComponent<Rigidbody>());
    var first = entities.Where(x => (BodyID)x.GetComponent<Rigidbody>().BodyInterface.BodyId == body1).FirstOrDefault();
    var second = entities.Where(x => (BodyID)x.GetComponent<Rigidbody>().BodyInterface.BodyId == body2).FirstOrDefault();

    return (first, second);
  }

  public void Dispose() {
    if (!_bodyInterface.IsNull) {
      _bodyInterface.DeactivateBody(_bodyID);
      // _bodyInterface.RemoveBody(_bodyID);
      // _bodyInterface.DestroyBody(_bodyID);
      _bodyInterface.RemoveAndDestroyBody(_bodyID);
    }
    GC.SuppressFinalize(this);
  }
}