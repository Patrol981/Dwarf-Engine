using System.Numerics;
using Dwarf.Math;
using Dwarf.Model;
using Dwarf.Model.Animation;

namespace Dwarf.Loaders;

public class FileVertex {
  public FileVector3? Position { get; set; }
  public FileVector3? Color { get; set; }
  public FileVector3? Normal { get; set; }
  public FileVector2? Uv { get; set; }

  public FileVector4I? JointIndices { get; set; }
  public FileVector4? JointWeights { get; set; }

  public static FileVertex ToFileVertex(Vertex vertex) {
    return new FileVertex {
      Position = FileVector3.GetFileVector3(vertex.Position),
      Color = FileVector3.GetFileVector3(vertex.Color),
      Normal = FileVector3.GetFileVector3(vertex.Normal),
      Uv = FileVector2.GetFileVector2(vertex.Uv),

      JointIndices = FileVector4I.GetFileVector4I(vertex.JointIndices),
      JointWeights = FileVector4.GetFileVector4(vertex.JointWeights),
    };
  }

  public static List<FileVertex> GetFileVertices(Vertex[] vertices) {
    return vertices.Select(x => FileVertex.ToFileVertex(x)).ToList();
  }
}

public class FileMesh {
  public List<FileVertex>? Vertices { get; set; }
  public List<uint>? Indices { get; set; }
  public ulong VertexCount { get; set; }
  public ulong IndexCount { get; set; }

  public static FileMesh ToFileMesh(Mesh mesh) {
    return new FileMesh {
      Vertices = FileVertex.GetFileVertices(mesh.Vertices),
      Indices = [.. mesh.Indices],
      VertexCount = mesh.VertexCount,
      IndexCount = mesh.IndexCount,
    };
  }
}

public class FileNode {
  public FileNode? Parent { get; set; }
  public int Index { get; set; }
  public List<FileNode>? Children { get; set; }
  public string Name { get; set; } = string.Empty;
  public FileMesh? Mesh { get; set; }
  public FileSkin? Skin { get; set; }
  public int SkinIndex { get; set; }
  public FileVector3? Translation { get; set; }
  public FileQuaternion? Rotation { get; set; }
  public FileVector3? Scale { get; set; }

  public static FileNode ToFileNode(Node node) {
    if (node == null) return null!;

    var fileNode = new FileNode {
      Index = node.Index,
      SkinIndex = node.SkinIndex,
      Translation = FileVector3.GetFileVector3(node.Translation),
      Rotation = FileQuaternion.GetFileQuaternion(node.Rotation),
      Scale = FileVector3.GetFileVector3(node.Scale)
    };

    if (node.Children != null && node.Children.Count != 0) {
      fileNode.Children = [];
      foreach (var childNode in node.Children) {
        fileNode.Children!.Add(ToFileNode(childNode));
      }
    }

    if (node.HasSkin) {
      fileNode.Skin = FileSkin.ToFileSkin(node.Skin!);
    }

    if (node.HasMesh) {
      fileNode.Mesh = FileMesh.ToFileMesh(node.Mesh!);
    }

    return fileNode;
  }

  public static List<FileNode> ToFileNodes(List<Node> nodes) {
    List<FileNode> fileNodes = [];

    foreach (Node node in nodes) {
      fileNodes.Add(ToFileNode(node));
    }

    return fileNodes;
  }
}

public class FileSkin {
  public string Name { get; set; } = default!;
  public FileNode? SkeletonRoot { get; set; }
  public List<FileMatrix4x4>? InverseBindMatrices { get; set; }
  public List<FileNode>? Joints { get; set; }
  public List<FileMatrix4x4>? OutputNodeMatrices { get; set; }
  public int JointsCount { get; set; }

  public static FileSkin ToFileSkin(Skin skin) {
    return new FileSkin {
      Name = skin.Name,
      SkeletonRoot = skin.SkeletonRoot != null ? FileNode.ToFileNode(skin.SkeletonRoot) : null,
      InverseBindMatrices = FileMatrix4x4.GetFileMatrices(skin.InverseBindMatrices),
      Joints = FileNode.ToFileNodes(skin.Joints),
      OutputNodeMatrices = FileMatrix4x4.GetFileMatrices([.. skin.OutputNodeMatrices]),
      JointsCount = skin.JointsCount
    };
  }

  public static List<FileSkin> ToFileSkins(List<Skin> skins) {
    return skins.Select(skin => ToFileSkin(skin)).ToList();
  }
}

public class FileMatrix4x4 {
  public float[] Values { get; set; } = [];

  public static FileMatrix4x4 GetFileMatrix4X4(Matrix4x4 matrix4) {
    return new FileMatrix4x4 {
      Values = [
        matrix4.M11, matrix4.M12, matrix4.M13, matrix4.M14,
        matrix4.M21, matrix4.M22, matrix4.M23, matrix4.M24,
        matrix4.M31, matrix4.M32, matrix4.M33, matrix4.M34,
        matrix4.M41, matrix4.M42, matrix4.M43, matrix4.M44,
      ]
    };
  }

  public static List<FileMatrix4x4> GetFileMatrices(List<Matrix4x4> matrices) {
    return matrices.Select(mat => { return GetFileMatrix4X4(mat); }).ToList();
  }
}

public class FileVector3 {
  public float[] Values { get; set; } = [];

  public static FileVector3 GetFileVector3(Vector3 vector3) {
    return new FileVector3 {
      Values = [vector3.X, vector3.Y, vector3.Z]
    };
  }
}

public class FileVector2 {
  public float[] Values { get; set; } = [];

  public static FileVector2 GetFileVector2(Vector2 vector2) {
    return new FileVector2 {
      Values = [vector2.X, vector2.Y]
    };
  }
}

public class FileVector4 {
  public float[] Values { get; set; } = [];

  public static FileVector4 GetFileVector4(Vector4 vector4) {
    return new FileVector4 {
      Values = [vector4.X, vector4.Y, vector4.Z, vector4.W]
    };
  }
}

public class FileVector4I {
  public int[] Values { get; set; } = [];

  public static FileVector4I GetFileVector4I(Vector4I vector4) {
    return new FileVector4I {
      Values = [vector4.X, vector4.Y, vector4.Z, vector4.W]
    };
  }
}

public class FileQuaternion {
  public float[] Values { get; set; } = [];

  public static FileQuaternion GetFileQuaternion(Quaternion quaternion) {
    return new FileQuaternion {
      Values = [quaternion.X, quaternion.Y, quaternion.Z, quaternion.W]
    };
  }
}

public class FileAnimation {
  public string Name { get; set; } = string.Empty;
  public List<AnimationSampler> Samplers { get; set; } = [];
  public List<AnimationChannel> Channels { get; set; } = [];
  public float Start { get; set; }
  public float End { get; set; }

  public static FileAnimation ToFileAnimation(Animation animation) {
    return new FileAnimation {
      Name = animation.Name,
      Samplers = animation.Samplers,
      Channels = animation.Channels,
      Start = animation.Start,
      End = animation.End,
    };
  }

  public static List<FileAnimation> ToFileAnimations(List<Animation> animations) {
    return animations.Select(anim => ToFileAnimation(anim)).ToList();
  }
}

public class DwarfFile {
  public string FileName { get; set; } = string.Empty;
  public int TextureFlipped { get; set; }
  public List<FileNode>? Nodes { get; set; }
  public List<FileNode>? LinearNodes { get; set; }
  public List<FileNode>? MeshedNodes { get; set; }
  public List<FileAnimation>? Animations { get; set; }
  public List<FileSkin>? Skins { get; set; }
  public List<FileMatrix4x4>? InverseMatrices { get; set; }

  public List<VulkanTexture>? Textures { get; set; }

  public static DwarfFile ToDwarfFile(MeshRenderer meshRenderer) {
    return new DwarfFile {
      FileName = meshRenderer.FileName,
      TextureFlipped = meshRenderer.TextureFlipped,

      Nodes = FileNode.ToFileNodes([.. meshRenderer.Nodes]),
      LinearNodes = FileNode.ToFileNodes([.. meshRenderer.LinearNodes]),
      MeshedNodes = FileNode.ToFileNodes([.. meshRenderer.MeshedNodes]),

      Animations = FileAnimation.ToFileAnimations(meshRenderer.Animations),
      Skins = FileSkin.ToFileSkins(meshRenderer.Skins),
      InverseMatrices = FileMatrix4x4.GetFileMatrices([.. meshRenderer.InverseMatrices])
    };
  }
}