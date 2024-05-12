using System.Numerics;

namespace Dwarf.Math;

public static class Converter {
  public static float DegreesToRadians(float deg) {
    float rad = MathF.PI / 180 * deg;
    return rad;
  }

  public static float RadiansToDegrees(float rad) {
    float deg = 180 / MathF.PI * rad;
    return deg;
  }

  public static Vector4I ToVec4I(this Vector4 vec4) {
    return new Vector4I((int)vec4.X, (int)vec4.Y, (int)vec4.Z, (int)vec4.W);
  }
}