using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Dwarf.Engine.Math;
public class AABB {
  private Vector3 _min;
  private Vector3 _max;

  public AABB() {
    _min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    _max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
  }

  public void Update(Mesh mesh) {
    foreach (var item in mesh.Vertices) {
      _min.X = MathF.Min(_min.X, item.Position.X);
      _min.Y = MathF.Min(_min.Y, item.Position.Y);
      _min.Z = MathF.Min(_min.Z, item.Position.Z);

      _max.X = MathF.Max(_max.X, item.Position.X);
      _max.Y = MathF.Max(_max.Y, item.Position.Y);
      _max.Z = MathF.Max(_max.Z, item.Position.Z);
    }
  }

  public void Update(AABB[] aabbes) {
    foreach (var aabb in aabbes) {
      _min.X = MathF.Min(_min.X, aabb.Min.X);
      _min.Y = MathF.Min(_min.Y, aabb.Min.Y);
      _min.Z = MathF.Min(_min.Z, aabb.Min.Z);

      _max.X = MathF.Max(_max.X, aabb.Max.X);
      _max.Y = MathF.Max(_max.Y, aabb.Max.Y);
      _max.Z = MathF.Max(_max.Z, aabb.Max.Z);
    }
  }

  public Vector3 Min {
    get { return _min; }
    set { _min = value; }
  }

  public Vector3 Max {
    get { return _max; }
    set { _max = value; }
  }
}
