using System.Numerics;

namespace Dwarf.Rendering.Shadows;

public struct ShadowObject {
  public Vector3 Position;
  public float Radius;
  public Mesh ShadowMesh;
}