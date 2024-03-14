using System.Numerics;

namespace Dwarf.Rendering.Lightning;
public class DirectionalLight {
  public Vector3 LightPosition = new(0, -5, 0);
  public Vector3 LightDirection = new(0, 0, 0);
  public Vector4 LightColor = new(1, 1, 1, 1);
  public Vector4 AmbientColor = new(1, 1, 1, 1);
}
