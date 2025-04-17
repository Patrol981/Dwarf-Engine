using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Hammer;
using Dwarf.Hammer.Models;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering;

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
    _bodyId = _hammerInterface.CreateAndAddBody(Dwarf.Hammer.Enums.MotionType.Dynamic, Vector2.Zero);

    return null!;
  }

  public object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    throw new NotImplementedException();
  }

  public void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector2 position) {
    _bodyId = _hammerInterface.CreateAndAddBody((Dwarf.Hammer.Enums.MotionType)motionType, position);
  }

  public void SetActive(bool value) {
    throw new NotImplementedException();
  }

  public void AddForce(Vector2 force) {
    throw new NotImplementedException();
  }

  public void AddLinearVelocity(Vector2 velocity) {
    throw new NotImplementedException();
  }

  public void AddImpulse(Vector2 impulse) {
    throw new NotImplementedException();
  }

  public void Dispose() {
    throw new NotImplementedException();
  }
}