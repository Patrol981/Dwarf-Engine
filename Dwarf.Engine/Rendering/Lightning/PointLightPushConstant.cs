using System.Numerics;

namespace Dwarf.Rendering.Lightning;
public struct PointLightPushConstant {
  public Vector4 Position;
  public Vector4 Color;
  public float Radius;
}
