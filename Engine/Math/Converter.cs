namespace Dwarf.Engine.Math;

public static class Converter {
  public static float DegreesToRadians(float deg) {
    float rad = (MathF.PI / 180) * deg;
    return (rad);
  }
}