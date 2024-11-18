using Dwarf.Math;

namespace Dwarf.Physics;
public interface ICollision {
  public AABB[] AABBArray { get; }
  public AABB AABB { get; }
}
