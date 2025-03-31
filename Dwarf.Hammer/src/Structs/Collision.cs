namespace Dwarf.Hammer;

public struct Collision {
  public HammerObject A;
  public HammerObject B;
  public CollisionPoints CollisionPoints;

  public Collision(HammerObject objA, HammerObject objB, CollisionPoints collisionPoints) {
    A = objA;
    B = objB;
    CollisionPoints = collisionPoints;
  }
}