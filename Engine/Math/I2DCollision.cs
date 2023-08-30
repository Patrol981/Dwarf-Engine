using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;

namespace Dwarf.Engine.Math;
public interface I2DCollision {
  public bool IsUI { get; }
  public Vector2 Size { get; }
  public Bounds2D Bounds { get; }
}
