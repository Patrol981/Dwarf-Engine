using System.Numerics;

namespace Dwarf.Rendering.Particles;

public struct ParticlePushConstant {
  public Vector4 Position;
  public Vector4 Color;
  public float Radius;
}