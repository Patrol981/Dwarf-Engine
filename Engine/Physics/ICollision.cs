using Dwarf.Engine.Math;

namespace Dwarf.Engine.Physics;
public interface ICollision {
  public AABB[] AABBArray { get; }
  public AABB AABB { get; }
}
