namespace Dwarf.Hammer.Colliders;

public abstract class Collider {
  public abstract CollisionPoints TestCollision(
    Transform transform,
    Collider other,
    Transform otherTransform
  );

  public abstract CollisionPoints TestCollision(
    Transform transform,
    SphereCollider sphere,
    Transform sphereTransform
  );

  public abstract CollisionPoints TestCollision(
    Transform transform,
    PlaneCollider plane,
    Transform planeTransform
  );

  public abstract CollisionPoints TestCollision(
    Transform transform,
    BoxCollider box,
    Transform boxTransform
  );

  public static CollisionPoints FindSphereSphereCollisionPoints(
    SphereCollider a,
    SphereCollider b,
    Transform ta,
    Transform tb
  ) {
    throw new NotImplementedException();
  }

  public static CollisionPoints FindSpherePlaneCollisionPoints(
    SphereCollider a,
    PlaneCollider b,
    Transform ta,
    Transform tb
  ) {
    throw new NotImplementedException();
  }

  public static CollisionPoints FindPlaneSphereCollisionPoints(
    PlaneCollider a,
    SphereCollider b,
    Transform ta,
    Transform tb
  ) {
    throw new NotImplementedException();
  }
}