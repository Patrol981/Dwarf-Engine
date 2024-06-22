using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf.Rendering.Lightning;

[StructLayout(LayoutKind.Sequential)]
public struct PointLight {
  public Vector4 LightColor;
  public Vector3 LightPosition;

  public static PointLight New() {
    return new PointLight {
      LightColor = new(1, 1, 1, 1),
      LightPosition = new(0, 0, 0)
    };
  }
}
