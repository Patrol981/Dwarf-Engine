namespace Dwarf.Hammer.Colliders;

public class BoxCollider : Collider {
  public override CollisionPoints TestCollision(Transform transform, Collider other, Transform otherTransform) {
    throw new NotImplementedException();
  }

  public override CollisionPoints TestCollision(Transform transform, SphereCollider sphere, Transform sphereTransform) {
    throw new NotImplementedException();
  }

  public override CollisionPoints TestCollision(Transform transform, PlaneCollider plane, Transform planeTransform) {
    throw new NotImplementedException();
  }

  public override CollisionPoints TestCollision(Transform transform, BoxCollider box, Transform boxTransform) {
    throw new NotImplementedException();
  }
}