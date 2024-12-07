using System.Numerics;

namespace Dwarf.Hammer.Colliders;

public class SphereCollider : Collider {
  public Vector3 Center { get; set; }
  public float Radius { get; set; }

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
    return FindSphereSphereCollisionPoints(this, sphere, transform, sphereTransform);
  }

  public override CollisionPoints TestCollision(
    Transform transform,
    PlaneCollider plane,
    Transform planeTransform
  ) {
    return FindSpherePlaneCollisionPoints(this, plane, transform, planeTransform);
  }

  public override CollisionPoints TestCollision(
    Transform transform,
    BoxCollider box,
    Transform boxTransform
  ) {
    throw new NotImplementedException();
  }
}