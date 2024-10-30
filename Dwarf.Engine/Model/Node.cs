using System.Numerics;
using Dwarf.Math;
using Dwarf.Model.Animation;

namespace Dwarf.Model;

public class Node {
  public const int MAX_NUM_JOINTS = 128;

  public Node? Parent;
  public int Index = 0;
  public List<Node> Children = [];
  public Matrix4x4 NodeMatrix = Matrix4x4.Identity;
  public string Name = string.Empty;
  public Mesh? Mesh;
  public Skin? Skin;
  public int SkinIndex = -1;
  public Vector3 Translation = Vector3.Zero;
  public Quaternion Rotation = Quaternion.Identity;
  public Vector3 Scale = Vector3.One;
  public bool UseCachedMatrix = false;
  public Matrix4x4 CachedLocalMatrix = Matrix4x4.Identity;
  public Matrix4x4 CachedMatrix = Matrix4x4.Identity;

  public MeshRenderer ParentRenderer = null!;
  public BoundingBox BoundingVolume;
  public BoundingBox AABB;

  public float AnimationTimer = 0.0f;

  public Matrix4x4 GetLocalMatrix() {
    if (!UseCachedMatrix) {
      CachedLocalMatrix =
        NodeMatrix *
        Matrix4x4.CreateScale(Scale) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateTranslation(Translation);
    }
    return CachedLocalMatrix;
  }

  public Matrix4x4 GetMatrix() {
    if (!UseCachedMatrix) {
      Matrix4x4 m = GetLocalMatrix();
      var p = Parent;
      while (p != null) {
        m *= p.GetLocalMatrix();
        p = p.Parent;
      }
      CachedMatrix = m;
      UseCachedMatrix = true;
      return m;
    } else {
      return CachedMatrix;
    }
  }

  public Task DrawNode(IntPtr commandBuffer, uint firstInstance = 0) {
    if (!HasMesh) return Task.CompletedTask;

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.DrawIndexed(commandBuffer, Mesh!.IndexCount, 1, 0, 0, firstInstance);
    } else {
      ParentRenderer.Renderer.CommandList.Draw(commandBuffer, Mesh!.VertexCount, 1, 0, firstInstance);
    }
    return Task.CompletedTask;
  }

  public Task BindNode(IntPtr commandBuffer) {
    if (!HasMesh) return Task.CompletedTask;

    ParentRenderer.Renderer.CommandList.BindVertex(commandBuffer, Mesh!.VertexBuffer!, 0);

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.BindIndex(commandBuffer, Mesh!.IndexBuffer!);
    }

    return Task.CompletedTask;
  }

  public void Update() {
    UseCachedMatrix = false;
    if (Mesh != null) {
      Matrix4x4 m = GetMatrix();
      if (Skin != null) {
        Mesh.Matrix = m;
        Matrix4x4.Invert(m, out var inTransform);
        int numJoints = (int)MathF.Min(Skin.Joints.Count, MAX_NUM_JOINTS);
        for (int i = 0; i < numJoints; i++) {
          var jointNode = Skin.Joints[i];
          var jointMat = Skin.InverseBindMatrices[i] * inTransform * jointNode.GetMatrix();
          Skin.OutputNodeMatrices[i] = jointMat;
        }
        Skin.JointsCount = numJoints;
      } else {
        Mesh.Matrix = m;
      }
    }

    foreach (var child in Children) {
      child.Update();
    }
  }

  public bool HasMesh => Mesh != null;
  public bool HasSkin => Skin != null;
  public void Dispose() {
    Skin?.Dispose();
    Mesh?.Dispose();
  }
}