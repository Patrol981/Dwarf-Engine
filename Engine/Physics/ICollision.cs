using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.Math;

namespace Dwarf.Engine.Physics;
public interface ICollision {
  public AABB[] AABBArray { get; }
  public AABB AABB { get; }
}
