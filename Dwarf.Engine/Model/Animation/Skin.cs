using System.Numerics;

namespace Dwarf.Model.Animation;

public class Skin : IDisposable {
  public string Name { get; set; } = default!;

  public Node SkeletonRoot = null!;
  public List<Matrix4x4> InverseBindMatrices = null!;
  public List<Node> Joints = [];

  public Matrix4x4[] OutputNodeMatrices = [];
  public int JointsCount;

  public Skin() {
  }

  public void Init() {
    OutputNodeMatrices = new Matrix4x4[Joints.Count];
    for (int i = 0; i < OutputNodeMatrices.Length; i++) {
      OutputNodeMatrices[i] = Matrix4x4.Identity;
    }
  }
  public void Dispose() {
  }
}
