using System.Numerics;
using Dwarf.Model.Animation;

namespace Dwarf;

public class Mesh : IDisposable {
  public Vertex[] Vertices = [];
  public uint[] Indices = [];
  public Vector4[] BoneIndices = [];
  public Vector4[] BoneWeights = [];

  public Matrix4x4[] NodeMatrices = [];

  public Skin? Skin = default!;

  public float Height {
    get {
      double minY = double.MaxValue;
      double maxY = double.MinValue;

      foreach (var v in Vertices) {
        if (v.Position.Y < minY)
          minY = v.Position.Y;
        if (v.Position.Y > maxY)
          maxY = v.Position.Y;
      }

      return (float)(maxY - minY);
    }
  }

  public void Dispose() {
    Skin?.Dispose();
  }
}