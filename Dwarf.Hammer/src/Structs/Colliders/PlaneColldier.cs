using System.Numerics;

namespace Dwarf.Hammer.Colliders;

public class PlaneCollider : Collider {
  public Vector3 Plane { get; set; }
  public float Distance { get; set; }

  public override CollisionPoints TestCollision(
    Transform transform,
    Collider other,
    Transform otherTransform
  ) {
    return TestCollision(otherTransform, this, transform);
  }

  public override CollisionPoints TestCollision(
    Transform transform,
    SphereCollider sphere,
    Transform sphereTransform
  ) {
    return FindPlaneSphereCollisionPoints(this, sphere, transform, sphereTransform);
  }

  public override CollisionPoints TestCollision(
    Transform transform,
    PlaneCollider plane,
    Transform planeTransform
  ) {
    throw new NotImplementedException();
  }

  public override CollisionPoints TestCollision(
    Transform transform,
    BoxCollider box,
    Transform boxTransform
  ) {
    throw new NotImplementedException();
  }
}