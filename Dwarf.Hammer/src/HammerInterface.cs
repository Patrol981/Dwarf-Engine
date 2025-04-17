using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerInterface {
  private readonly HammerWorld _hammerWorld;

  public HammerInterface(HammerWorld hammerWorld) {
    _hammerWorld = hammerWorld;
  }

  public Vector2 GetPosition(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].Position;
  }

  public void SetPosition(in BodyId bodyId, in Vector2 position) {
    _hammerWorld.Bodies[bodyId].Position = position;
  }

  public void SetVelocity(in BodyId bodyId, in Vector2 velocity) {
    _hammerWorld.Bodies[bodyId].Velocity = velocity;
  }

  public Vector2 GetVelocity(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].Velocity;
  }

  public void SetGravity(float gravity) {
    _hammerWorld.Gravity = gravity;
  }

  public float GetGravity() {
    return _hammerWorld.Gravity;
  }

  public void SetMotionType(in BodyId bodyId, MotionType motionType) {
    _hammerWorld.Bodies[bodyId].MotionType = motionType;
  }

  public MotionType GetMotionType(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].MotionType;
  }

  public void SetMotionQuality(in BodyId bodyId, MotionQuality motionQuality) {
    _hammerWorld.Bodies[bodyId].MotionQuality = motionQuality;
  }

  public MotionQuality GetMotionQuality(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].MotionQuality;
  }

  public BodyId CreateAndAddBody(MotionType motionType, Vector2 position) {
    var body = _hammerWorld.AddBody(position);
    _hammerWorld.Bodies[body].MotionType = motionType;
    return body;
  }
}