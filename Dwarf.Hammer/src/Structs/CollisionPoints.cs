using System.Numerics;

namespace Dwarf.Hammer;

public struct CollisionPoints {
  public Vector3 A;
  public Vector3 B;
  public Vector3 Normal;
  public float Depth;
  public bool HasCollision;
}