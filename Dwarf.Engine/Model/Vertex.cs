using System.Numerics;

namespace Dwarf;

public struct Vertex {
  public Vector3 Position;
  public Vector3 Color;
  public Vector3 Normal;
  public Vector2 Uv;

  public Vector4 JointIndices;
  public Vector4 JointWeights;
}

public struct SimpleVertex {
  public Vector3 Position;
  public Vector3 Color;
}

public struct TexturedVertex {
  public Vector3 Position;
  public Vector3 Color;
  public Vector2 Uv;
}