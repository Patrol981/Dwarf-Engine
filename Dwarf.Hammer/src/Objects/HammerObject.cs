using System.Numerics;
using Dwarf.Hammer.Colliders;

namespace Dwarf.Hammer;

public class HammerObject {
  public Collider? Collider;
  public Transform Transform;

  public bool IsDynamic;
  public bool IsTrigger;
}