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
}