using System.Numerics;
using System.Text.Json.Serialization;
using Dwarf.AbstractionLayer;
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

  public static Vertex FromFileVertex(FileVertex vertex) {
    return new Vertex {
      Position = vertex.Position?.Values.Length > 0 ? FileVector3.ParseFileVector3(vertex.Position!) : Vector3.Zero,
      Color = vertex.Color?.Values.Length > 0 ? FileVector3.ParseFileVector3(vertex.Color!) : Vector3.Zero,
      Normal = vertex.Normal?.Values.Length > 0 ? FileVector3.ParseFileVector3(vertex.Normal!) : Vector3.Zero,
      Uv = vertex.Uv?.Values.Length > 0 ? FileVector2.ParseFileVector2(vertex.Uv!) : Vector2.Zero,

      JointIndices = vertex.JointIndices?.Values.Length > 0 ? FileVector4I.ParseFileVector4I(vertex.JointIndices) : new(0, 0, 0, 0),
      JointWeights = vertex.JointWeights?.Values.Length > 0 ? FileVector4.ParseFileVector4(vertex.JointWeights) : Vector4.Zero
    };
  }

  public static List<FileVertex> GetFileVertices(Vertex[] vertices) {
    return vertices.Select(x => FileVertex.ToFileVertex(x)).ToList();
  }

  public static Vertex[] FromFileVertices(List<FileVertex> fileVertices) {
    return fileVertices.Select(x => FileVertex.FromFileVertex(x)).ToArray();
  }
}

public class FileMesh {
  public List<FileVertex>? Vertices { get; set; }
  public List<uint>? Indices { get; set; }
  public ulong VertexCount { get; set; }
  public ulong IndexCount { get; set; }

  [JsonIgnore] public ITexture? Texture { get; set; }

  public string? BinaryReferenceName { get; set; }
  public ulong BinaryOffset { get; set; }
  public ulong BinaryTextureSize { get; set; }
  public string TextureFileName { get; set; } = string.Empty;

  public static byte[] GetTextureDataOutOfId(TextureManager textureManager, Guid texId) {
    if (texId == Guid.Empty) return null!;

    var tex = textureManager.GetTexture(texId);
    return tex.TextureData;
  }

  public static ITexture GetTextureOutOfId(TextureManager textureManager, Guid texId) {
    if (texId == Guid.Empty) return null!;

    return textureManager.GetTexture(texId);
  }

  public static Guid CreateTextureFromFile(string fileName) {
    return Guid.Empty;
  }

  public static FileMesh ToFileMesh(Mesh mesh) {
    return new FileMesh {
      Vertices = FileVertex.GetFileVertices(mesh.Vertices),
      Indices = [.. mesh.Indices],
      VertexCount = mesh.VertexCount,
      IndexCount = mesh.IndexCount,
      Texture = GetTextureOutOfId(Application.Instance.TextureManager, mesh.TextureIdReference)
    };
  }

  public static Mesh FromFileMesh(FileMesh fileMesh) {
    var mesh = new Mesh(Application.Instance.Device) {
      Vertices = fileMesh.Vertices?.Count > 0 ? FileVertex.FromFileVertices(fileMesh.Vertices) : null!,
      Indices = fileMesh.Indices?.Count > 0 ? [.. fileMesh.Indices] : null!,
      VertexCount = fileMesh.VertexCount,
      IndexCount = fileMesh.IndexCount,
    };

    return mesh;
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

  public static Node FromFileNode(FileNode fileNode, Node parent = null!) {
    if (fileNode == null) return null!;

    var node = new Node {
      Index = fileNode.Index,
      SkinIndex = fileNode.SkinIndex,
      Translation = fileNode.Translation != null ? FileVector3.ParseFileVector3(fileNode.Translation) : Vector3.Zero,
      Rotation = fileNode.Rotation != null ? FileQuaternion.ParseQuaternion(fileNode.Rotation) : Quaternion.Identity,
      Scale = fileNode.Scale != null ? FileVector3.ParseFileVector3(fileNode.Scale) : Vector3.One,
    };

    if (parent != null) {
      node.Parent = parent;
    }

    if (fileNode.Skin != null) {
      node.Skin = FileSkin.FromFileSkin(fileNode.Skin!);
    }

    if (fileNode.Mesh != null) {
      node.Mesh = FileMesh.FromFileMesh(fileNode.Mesh!);
    }

    if (fileNode.Children != null && fileNode.Children.Count > 0) {
      foreach (var childNode in fileNode.Children) {
        node.Children.Add(FromFileNode(childNode, node));
      }
    }

    return node;
  }

  public static List<FileNode> ToFileNodes(List<Node> nodes) {
    List<FileNode> fileNodes = [];

    foreach (Node node in nodes) {
      fileNodes.Add(ToFileNode(node));
    }

    return fileNodes;
  }

  public static List<Node> FromFileNodes(List<FileNode> fileNodes) {
    List<Node> nodes = [];

    foreach (var fileNode in fileNodes) {
      nodes.Add(FromFileNode(fileNode));
    }

    return nodes;
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

  public static Skin FromFileSkin(FileSkin fileSkin) {
    return new Skin {
      Name = fileSkin.Name,
      SkeletonRoot = fileSkin.SkeletonRoot != null ? FileNode.FromFileNode(fileSkin.SkeletonRoot) : null!,
      InverseBindMatrices = fileSkin.InverseBindMatrices != null ? FileMatrix4x4.FromFileMatrices(fileSkin.InverseBindMatrices) : null!,
      Joints = fileSkin.Joints?.Count > 0 ? FileNode.FromFileNodes(fileSkin.Joints) : null!,
      OutputNodeMatrices = fileSkin.JointsCount > 0 ? [.. FileMatrix4x4.FromFileMatrices(fileSkin.OutputNodeMatrices!)] : null!,
      JointsCount = fileSkin.JointsCount
    };
  }

  public static List<FileSkin> ToFileSkins(List<Skin> skins) {
    return skins.Select(skin => ToFileSkin(skin)).ToList();
  }

  public static List<Skin> FromFileSkins(List<FileSkin> fileSkins) {
    return fileSkins.Select(skin => FromFileSkin(skin)).ToList();
  }
}

public class FileMatrix4x4 {
  public float[] Values { get; set; } = [];

  public static FileMatrix4x4 GetFileMatrix4x4(Matrix4x4 matrix4) {
    return new FileMatrix4x4 {
      Values = [
        matrix4.M11, matrix4.M12, matrix4.M13, matrix4.M14,
        matrix4.M21, matrix4.M22, matrix4.M23, matrix4.M24,
        matrix4.M31, matrix4.M32, matrix4.M33, matrix4.M34,
        matrix4.M41, matrix4.M42, matrix4.M43, matrix4.M44,
      ]
    };
  }

  public static Matrix4x4 FromFileMatrix4x4(FileMatrix4x4 matrix4) {
    return new Matrix4x4(
      matrix4.Values[0], matrix4.Values[1], matrix4.Values[2], matrix4.Values[3],
      matrix4.Values[4], matrix4.Values[5], matrix4.Values[6], matrix4.Values[7],
      matrix4.Values[8], matrix4.Values[9], matrix4.Values[10], matrix4.Values[11],
      matrix4.Values[12], matrix4.Values[13], matrix4.Values[14], matrix4.Values[15]
    );
  }

  public static List<FileMatrix4x4> GetFileMatrices(List<Matrix4x4> matrices) {
    return matrices.Select(mat => { return GetFileMatrix4x4(mat); }).ToList();
  }

  public static List<Matrix4x4> FromFileMatrices(List<FileMatrix4x4> matrices) {
    return matrices.Select(mat => { return FromFileMatrix4x4(mat); }).ToList();
  }
}

public class FileVector3 {
  public float[] Values { get; set; } = [];

  public static FileVector3 GetFileVector3(Vector3 vector3) {
    return new FileVector3 {
      Values = [vector3.X, vector3.Y, vector3.Z]
    };
  }

  public static Vector3 ParseFileVector3(FileVector3 fileVector3) {
    return new Vector3 {
      X = fileVector3.Values[0],
      Y = fileVector3.Values[1],
      Z = fileVector3.Values[2],
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

  public static Vector2 ParseFileVector2(FileVector2 fileVector2) {
    return new Vector2 {
      X = fileVector2.Values[0],
      Y = fileVector2.Values[1],
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

  public static Vector4 ParseFileVector4(FileVector4 fileVector4) {
    return new Vector4 {
      X = fileVector4.Values[0],
      Y = fileVector4.Values[1],
      Z = fileVector4.Values[2],
      W = fileVector4.Values[3],
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

  public static Vector4I ParseFileVector4I(FileVector4I fileVector4) {
    return new Vector4I {
      X = fileVector4.Values[0],
      Y = fileVector4.Values[1],
      Z = fileVector4.Values[2],
      W = fileVector4.Values[3],
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

  public static Quaternion ParseQuaternion(FileQuaternion fileQuaternion) {
    return new Quaternion {
      X = fileQuaternion.Values[0],
      Y = fileQuaternion.Values[1],
      Z = fileQuaternion.Values[2],
      W = fileQuaternion.Values[3],
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

  public static Animation FromFileAnimation(FileAnimation animation) {
    return new Animation {
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

  public static List<Animation> FromFileAnimations(List<FileAnimation> animations) {
    return animations.Select(anim => FromFileAnimation(anim)).ToList();
  }
}

public class DwarfFile {
  public string BinaryDataRef { get; set; } = string.Empty;
  public string FileName { get; set; } = string.Empty;
  public int TextureFlipped { get; set; }
  public List<FileNode>? Nodes { get; set; }
  [JsonIgnore] public List<FileNode>? LinearNodes { get; set; }
  [JsonIgnore] public List<FileNode>? MeshedNodes { get; set; }
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