using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Structs;

namespace Dwarf.Hammer.Models;

internal class HammerObject {
  internal Vector2 Position;
  internal Vector2 Velocity;
  internal float Mass;
  internal MotionType MotionType;
  internal MotionQuality MotionQuality;
  internal ObjectType ObjectType;
  internal Mesh Mesh;
  internal AABB AABB = null!;
}