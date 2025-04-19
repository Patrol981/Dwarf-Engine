using System.Numerics;

namespace Dwarf.Hammer.Models;

internal class AABB {
  internal Vector2 Min;
  internal Vector2 Max;

  internal float Width => Max.X - Min.X;
  internal float Height => Max.Y - Min.Y;

  internal static AABB ComputeAABB(HammerObject hammerObject) {
    var verts = hammerObject.Mesh.Vertices;
    float minX = float.MaxValue, minY = float.MaxValue;
    float maxX = float.MinValue, maxY = float.MinValue;

    foreach (var v in verts) {
      minX = MathF.Min(minX, v.X);
      minY = MathF.Min(minY, v.Y);
      maxX = MathF.Max(maxX, v.X);
      maxY = MathF.Max(maxY, v.Y);
    }

    return new AABB {
      Min = new Vector2(minX, minY),
      Max = new Vector2(maxX, maxY)
    };
  }

  internal bool CheckCollision(Vector2 aPos, Vector2 bPos, AABB bAABB) {
    bool collX = aPos.X + Width >= bPos.X && bPos.X + bAABB.Width >= aPos.X;
    bool collY = aPos.Y + Height >= bPos.Y && bPos.Y + bAABB.Height >= aPos.Y;

    return collX && collY;
  }
}

