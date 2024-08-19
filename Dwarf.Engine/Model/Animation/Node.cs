using System.Numerics;

namespace Dwarf.Model.Animation;

public class Node {
  public const int MAX_NUM_JOINTS = 128;

  public Node? Parent;
  public int Index;
  public List<Node> Children = [];
  public Matrix4x4 Matrix;
  public string Name = string.Empty;
  public Mesh Mesh;
  public Skin Skin;
  public int SkinIndex = -1;
  public Vector3 Translation;
  public Quaternion Rotation;
  public Vector3 Scale;
  public bool UseCachedMatrix = false;
  public Matrix4x4 CachedLocalMatrix = Matrix4x4.Identity;
  public Matrix4x4 CachedMatrix = Matrix4x4.Identity;

  public Matrix4x4 GetLocalMatrix() {
    if (!UseCachedMatrix) {
      CachedLocalMatrix =
        Matrix4x4.CreateTranslation(Translation) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateScale(Scale);
    }
    return CachedLocalMatrix;
  }

  public Matrix4x4 GetMatrix() {
    if (!UseCachedMatrix) {
      Matrix4x4 m = GetLocalMatrix();
      var p = Parent;
      while (p != null) {
        m = p.GetLocalMatrix() * m;
        p = p.Parent;
      }
      CachedMatrix = m;
      UseCachedMatrix = true;
      return m;
    } else {
      return CachedMatrix;
    }
  }

  public void Update() {
    UseCachedMatrix = false;
    Matrix4x4 m = GetMatrix();
    if (Skin != null) {
      var outputMatrix = m;

      // Update Joint Matrices
      Matrix4x4.Invert(m, out var inTransform);
      int numJoints = (int)MathF.Min(Skin.Joints.Count, MAX_NUM_JOINTS);
      for (int i = 0; i < numJoints; i++) {
        var jointNode = Skin.Joints[i];
        var jointMat = jointNode.GetMatrix() * Skin.InverseBindMatrices[i];
        jointMat = inTransform * jointMat;
        Skin.OutputNodeMatrices[i] = outputMatrix * jointMat;
      }
      Skin.JointsCount = numJoints;
      Skin.WriteSkeleton();
    }

    foreach (var child in Children) {
      child.Update();
    }
  }
}