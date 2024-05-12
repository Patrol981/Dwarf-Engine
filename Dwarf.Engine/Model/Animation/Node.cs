using System.Numerics;

namespace Dwarf.Model.Animation;
public unsafe class Node {
  public Node? Parent { get; private set; }
  public uint Index { get; private set; }
  public Node[]? ChildNodes { get; private set; }
  public Mesh? Mesh { get; private set; }
  public Vector3 Translation { get; private set; }
  public Vector3 Scale { get; private set; }
  public Quaternion Rotation { get; private set; }
  public int Skin { get; private set; } = -1;
  public Matrix4x4 Matrix { get; private set; }

  public Matrix4x4 GetLocal() {
    return Matrix4x4.Identity;
  }
}
