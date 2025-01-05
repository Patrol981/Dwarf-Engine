using System.Numerics;
using Dwarf.EntityComponentSystem;

namespace Dwarf.Physics;

public interface IPhysicsBody : IDisposable {
  Vector3 Position { get; set; }
  Quaternion Rotation { get; set; }
  Vector3 LinearVelocity { get; set; }
  Vector3 AngularVelocity { get; set; }
  float GravityFactor { get; set; }
  MotionQuality MotionQuality { get; set; }
  MotionType MotionType { get; set; }
  object CreateAndAddBody(object settings);
  object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh);
  object BodyId { get; }
  void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector3 position);
  void AddForce(Vector3 force);
  void AddLinearVelocity(Vector3 velocity);
  void AddImpulse(Vector3 impulse);
}