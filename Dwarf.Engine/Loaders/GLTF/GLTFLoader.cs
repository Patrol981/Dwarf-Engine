using System.Numerics;

using Dwarf.Extensions.Logging;

using SharpGLTF.Schema2;

namespace Dwarf.Loader.Providers;

public partial class GLTFLoader {
  public static async Task<MeshRenderer> Load(Application app, string path, bool preload = false, int flip = 1) {
    var materialTexturePair = new Dictionary<int, int>();
    var modelRoot = ModelRoot.Load(path);
    var modelMeshes = ProcessModel(modelRoot);

    /*
    foreach (var mesh in model.LogicalMeshes) {
      var resultMesh = ProcessMesh(mesh);
      if (resultMesh != null) {
        meshes.Add(resultMesh);
      }
    }
    

    foreach (var bone in model.LogicalSkins) {
      ProcessBones(bone);
    }

    foreach (var anim in model.LogicalAnimations) {

    }
    */

    var resultModel = new MeshRenderer(app.Device, app.Renderer, modelMeshes.ToArray(), path);
    resultModel.TextureFlipped = flip;

    if (!preload) {
      var images = ProcessMaterials(modelRoot);
      string[] tags = GetFileNames(path, images.Count);
      string[] paths = new string[tags.Length];

      for (int i = 0; i < images.Count; i++) {
        File.WriteAllBytes($"./Resources/{tags[i]}.png", images[i]);
        paths[i] = $"./Resources/{tags[i]}.png";
      }

      var textures = await TextureManager.AddTextures(app.Device, paths, flip);
      app.TextureManager.AddRange([.. textures]);

      if (resultModel.MeshsesCount == images.Count || images.Count > resultModel.MeshsesCount) {
        resultModel.BindMultipleModelPartsToTextures(app.TextureManager, paths);
      } else if (paths.Length > 0) {
        resultModel.BindMultipleModelPartsToTexture(app.TextureManager, paths[0]);
      } else {
        resultModel.BindMultipleModelPartsToTexture(app.TextureManager, "./Resources/Textures/base/no_texture.png");
      }
    }

    return resultModel;
  }

  private static void ProcessAnimations(Animation animation) {
    // animation.
  }

  private static void ProcessBones(Skin skin) {
    if (skin == null) return;

    var boneTransfroms = new Dictionary<string, Matrix4x4>();
    var joints = skin.Skeleton;
  }

  private static IList<Dwarf.Mesh> ProcessModel(ModelRoot modelRoot) {
    var model = new List<Mesh>();

    foreach (var mesh in modelRoot.LogicalMeshes) {
      if (mesh == null) continue;

      var vertices = new List<Vertex>();
      var indices = new List<uint>();

      foreach (var primitive in mesh.Primitives) {
        var textureCoords = new List<Vector2>();
        var normals = new List<Vector3>();


        var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
        if (primitive.GetVertexAccessor("TEXCOORD_0") != null) {
          textureCoords = primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array().ToList();
        }
        if (primitive.GetVertexAccessor("NORMAL") != null) {
          normals = primitive.GetVertexAccessor("NORMAL").AsVector3Array().ToList();
        }
        var indexes = primitive.GetIndexAccessor().AsIndicesArray();
        indices.AddRange(indexes.ToArray());

        var vertex = new Vertex();
        for (int i = 0; i < positions.Count; i++) {
          vertex.Position = positions[i];
          vertex.Color = new Vector3(1, 1, 1);
          vertex.Normal = normals.Count > 0 ? normals[i] : new Vector3(1, 1, 1);
          vertex.Uv = textureCoords.Count > 0 ? textureCoords[i] : new Vector2(0, 0);

          vertices.Add(vertex);
        }
      }

      var meshData = new Mesh();
      meshData.Vertices = vertices.ToArray();
      meshData.Indices = indices.ToArray();
      model.Add(meshData);
    }

    if (modelRoot.LogicalSkins.Count > 0) {
      Logger.Info($"{modelRoot.LogicalMeshes.Count} : {modelRoot.LogicalSkins.Count}");

      foreach (var skin in modelRoot.LogicalSkins) {

      }
    }


    return model;
  }

  private static Dwarf.Mesh ProcessMesh(SharpGLTF.Schema2.Mesh mesh) {
    if (mesh == null) return null!;

    List<Vertex> vertices = new List<Vertex>();
    List<uint> indices = new List<uint>();

    foreach (var primitive in mesh.Primitives) {
      var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
      var textureCoords = primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array();
      var normals = primitive.GetVertexAccessor("NORMAL").AsVector3Array();
      // var colors = primitive.GetVertexAccessor("COLOR_0").AsVector4Array();
      var indexes = primitive.GetIndexAccessor().AsIndicesArray();

      indices.AddRange(indexes.ToArray());

      var vertex = new Vertex();
      for (int i = 0; i < positions.Count; i++) {
        vertex.Position = positions[i];
        // vertex.Color = new System.Numerics.Vector3(colors[i].X, colors[i].Y, colors[i].Z);
        vertex.Color = new System.Numerics.Vector3(1, 1, 1);
        vertex.Normal = normals[i];
        vertex.Uv = textureCoords[i];

        vertices.Add(vertex);
      }
    }

    var meshData = new Mesh();
    meshData.Vertices = vertices.ToArray();
    meshData.Indices = indices.ToArray();
    return meshData;
  }

  private static string GetFileName(string path) {
    string filename = string.Empty;

    try {
      filename = Path.GetFileName(path);
    } catch {
      int length = (int)MathF.Min(5, path.Length);
      filename = path.Substring(path.Length - length, length);
    }

    return filename;
  }

  private static string[] GetFileNames(string path, int imagesCount) {
    var filename = GetFileName(path);
    var filenames = new string[imagesCount];

    for (int i = 0; i < imagesCount; i++) {
      filenames[i] = $"{filename}{i + 1}";
    }

    return filenames;
  }

  private static List<byte[]> ProcessMaterials(ModelRoot model) {
    List<byte[]> images = new();

    foreach (var material in model.LogicalMaterials) {
      foreach (var channel in material.Channels) {
        if (channel.Texture == null) continue;

        var texture = channel.Texture;
        var image = texture.PrimaryImage;
        var imageStream = image.Content.Open();

        using var memoryStream = new MemoryStream();
        imageStream.CopyTo(memoryStream);
        byte[] imageData = memoryStream.ToArray();
        images.Add(imageData);
      }
    }

    return images;
  }

  [Obsolete]
  private static void ProcessNode(Node node, ref List<Mesh> meshes) {
    if (node.Mesh == null) return;

    List<Vertex> vertices = new List<Vertex>();
    List<uint> indices = new List<uint>();

    var mesh = node.Mesh;
    foreach (var primitive in mesh.Primitives) {
      var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
      var textureCoords = primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array();
      var normals = primitive.GetVertexAccessor("NORMAL").AsVector3Array();
      // var colors = primitive.GetVertexAccessor("COLOR_0").AsVector4Array();
      var indexes = primitive.GetIndexAccessor().AsIndicesArray();

      indices.AddRange(indexes.ToArray());

      var vertex = new Vertex();
      for (int i = 0; i < positions.Count; i++) {
        vertex.Position = positions[i];
        // vertex.Color = new System.Numerics.Vector3(colors[i].X, colors[i].Y, colors[i].Z);
        vertex.Color = new System.Numerics.Vector3(1, 1, 1);
        vertex.Normal = normals[i];
        vertex.Uv = textureCoords[i];

        // vertex.JointIndices = [];
        // vertex.JointWeights = [];

        vertices.Add(vertex);
      }
    }

    foreach (var childNode in node.VisualChildren) {
      // ProcessNode(childNode, vertices, indices);
      ProcessNode(childNode, ref meshes);
    }

    var meshData = new Mesh();
    meshData.Vertices = vertices.ToArray();
    meshData.Indices = indices.ToArray();
    meshes.Add(meshData);
  }
}