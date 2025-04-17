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
  private BodyId _bodyId;

  public HammerBodyWrapper(in HammerInterface hammerInterface) {
    _hammerInterface = hammerInterface;
  }

  public Vector2 Position {
    get => _hammerInterface.GetPosition(_bodyId);
    set => _hammerInterface.SetPosition(_bodyId, value);
  }
  public Vector2 LinearVelocity { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
  public Vector2 AngularVelocity { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
  public float GravityFactor {
    get => 0.0f;
    set => Logger.Info("");
  }
  public MotionQuality MotionQuality { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
  public MotionType MotionType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

  public object CreateAndAddBody(object settings) {
    throw new NotImplementedException();
  }

  public object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    throw new NotImplementedException();
  }

  public object BodyId => throw new NotImplementedException();

  public void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector2 position) {
    _bodyId = _hammerInterface.CreateAndAddBody(position);
    // throw new NotImplementedException();
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