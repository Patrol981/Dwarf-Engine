using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;

using SharpGLTF.Schema2;

namespace Dwarf.Loader.Providers;
public partial class GLTFLoader {
  public static async Task<MeshRenderer> LoadGLTF(
    Application app,
    string path,
    bool preload = false,
    int flip = 1
  ) {
    MeshRenderer meshRenderer;

    // Load Textures <TextureID, Texture>
    // Load Materials <MaterialID, Material>
    // Load Meshes

    var materials = new Dictionary<int, SharpGLTF.Schema2.Material>();
    var meshMaterialPair = new Dictionary<Mesh, SharpGLTF.Schema2.MaterialChannel>();

    var modelRoot = ModelRoot.Load(path);
    ProcessGLTF(app, modelRoot, ref meshMaterialPair, path);

    meshRenderer = new(app.Device, app.Renderer, [.. meshMaterialPair.Keys]);

    if (!preload) {
      var images = meshMaterialPair.Values.ToArray();
      string[] tags = GetFileNames(path, images.Length);
      string[] paths = new string[tags.Length];

      var iTextures = new List<ITexture>();

      for (int i = 0; i < images.Length; i++) {
        using var stream = images[i].Texture.PrimaryImage.Content.Open();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        byte[] imageData = memoryStream.ToArray();

        File.WriteAllBytes($"./Resources/{tags[i]}.png", imageData);
        // images[i].Texture.PrimaryImage.Content.SaveToFile($"./Resources/{tags[i]}.png");
        paths[i] = $"./Resources/{tags[i]}.png";

        /*
        var tex = TextureLoader.LoadFromBytes(
          app.Device,
          imageData,
          paths[i],
          flip
        );
        */
      }

      var textures = await TextureManager.AddTextures(app.Device, paths, flip);
      app.TextureManager.AddRange([.. textures]);

      if (meshRenderer.MeshsesCount == images.Length || images.Length > meshRenderer.MeshsesCount) {
        meshRenderer.BindMultipleModelPartsToTextures(app.TextureManager, paths);
      } else if (paths.Length > 0) {
        meshRenderer.BindMultipleModelPartsToTexture(app.TextureManager, paths[0]);
      } else {
        meshRenderer.BindMultipleModelPartsToTexture(app.TextureManager, "./Resources/Textures/base/no_texture.png");
      }
    }

    return meshRenderer;
  }

  private static void ProcessGLTF(
    Application app,
    ModelRoot modelRoot,
    ref Dictionary<Mesh, SharpGLTF.Schema2.MaterialChannel> meshMaterialPair,
    string path
  ) {
    // var idMeshPair = new Dictionary<int, SharpGLTF.Schema2.Mesh>();
    // var idMeshPair = new Dictionary<int, Mesh>();
    // int id = 0;

    // Logger.Info($"{path} skins : {modelRoot.LogicalSkins.Count}");
    // Logger.Info($"{path} nodes : {modelRoot.LogicalNodes.Count}");

    foreach (var node in modelRoot.LogicalNodes) {
      // Logger.Info($"{path} : {node.Name}");

      if (node.IsSkinSkeleton) {
        // ProcessArmatureData(app, modelRoot, node, ref meshMaterialPair, path);
      }

      if (node.Mesh != null) {
        ProcessMeshData(app, modelRoot, node, ref meshMaterialPair, path);
      }
    }

    if (path == "./Resources/astolfo.glb") {
      var test = modelRoot.GetJsonPreview();
      // Logger.Info($"{test.ToString()}");

      // Logger.Info(meshMaterialPair.Count);
    }
  }

  private static void ProcessArmatureData(
    Application app,
    ModelRoot modelRoot,
    Node node,
    ref Dictionary<Mesh, SharpGLTF.Schema2.MaterialChannel> meshMaterialPair,
    string path
  ) {
    Dwarf.Model.Animation.Skin skin = null!;
  }

  private static void ProcessMeshData(
    Application app,
    ModelRoot modelRoot,
    Node node,
    ref Dictionary<Mesh, SharpGLTF.Schema2.MaterialChannel> meshMaterialPair,
    string path
  ) {
    MaterialChannel baseColor = default;

    var vertices = new List<Vertex>();
    var indices = new List<uint>();
    var skinJoints = new List<Node>();

    foreach (var primitive in node.Mesh.Primitives) {
      var textureCoords = new List<Vector2>();
      var normals = new List<Vector3>();
      var weights = new List<Vector4>();
      var joints = new List<Vector4>();

      var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
      if (primitive.GetVertexAccessor("TEXCOORD_0") != null) {
        textureCoords = [.. primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array()];
      }
      if (primitive.GetVertexAccessor("NORMAL") != null) {
        normals = [.. primitive.GetVertexAccessor("NORMAL").AsVector3Array()];
      }
      if (primitive.GetVertexAccessor("WEIGHTS_0") != null) {
        weights = [.. primitive.GetVertexAccessor("WEIGHTS_0").AsVector4Array()];
      }
      if (primitive.GetVertexAccessor("JOINTS_0") != null) {
        joints = [.. primitive.GetVertexAccessor("JOINTS_0").AsVector4Array()];
      }
      var indexes = primitive.GetIndexAccessor().AsIndicesArray();
      indices.AddRange(indexes.ToArray());

      var vertex = new Vertex();
      for (int i = 0; i < positions.Count; i++) {
        vertex.Position = positions[i];
        vertex.Color = new Vector3(1, 1, 1);
        vertex.Normal = normals.Count > 0 ? normals[i] : new Vector3(1, 1, 1);
        vertex.Uv = textureCoords.Count > 0 ? textureCoords[i] : new Vector2(0, 0);

        vertex.JointWeights = weights.Count > 0 ? weights[i] : new Vector4(0, 0, 0, 0);
        vertex.JointIndices = joints.Count > 0 ? joints[i] : new Vector4(0, 0, 0, 0);

        if (node.Skin != null) {
          // var targetJoint = node.Skin.GetJoint(vertex.JointIndices);
        }


        vertices.Add(vertex);
      }

      var material = primitive.Material;
      if (material == null) continue;

      baseColor = material.FindChannel("BaseColor")!.Value;
    }

    Dwarf.Model.Animation.Skin skin = null!;

    if (node.Skin != null) {
      Logger.Info(node.Skin.GetInverseBindMatricesAccessor().LogicalIndex);

      unsafe {
        Matrix4x4[] inverseBindMatrices = [.. node.Skin.GetInverseBindMatricesAccessor().AsMatrix4x4Array()];

        /*
        Accessor? accessor = modelRoot.LogicalAccessors
          .Where(x => x.LogicalIndex == node.Skin.LogicalIndex).FirstOrDefault();
        BufferView? bufferView = modelRoot.LogicalBufferViews
          .Where(x => x.LogicalIndex == accessor?.LogicalIndex).FirstOrDefault();
        SharpGLTF.Schema2.Buffer? buffer = modelRoot.LogicalBuffers
          .Where(x => x.LogicalIndex == bufferView?.LogicalIndex).FirstOrDefault();
        

        if (accessor == null || buffer == null || bufferView == null) return;
        */

        skin = new Dwarf.Model.Animation.Skin.Builder()
          .SetName(node.Name)
          .SetInverseBindMatrices(inverseBindMatrices)
          .Build(app.Device);
        skin.Ssbo.Map(skin.Ssbo.GetAlignmentSize());
        skin.Write();
        skin.Ssbo.Unmap();

      }

    }

    if (vertices.Count < 1) {
      return;
    }

    var meshData = new Mesh {
      Vertices = [.. vertices],
      Indices = [.. indices],
      Skin = skin,
    };

    meshMaterialPair.Add(meshData, baseColor);
  }

  private static void ProcessGLTF_Old(
    ModelRoot modelRoot,
    ref Dictionary<Mesh, SharpGLTF.Schema2.MaterialChannel> meshMaterialPair
  ) {
    foreach (var mesh in modelRoot.LogicalMeshes) {
      MaterialChannel baseColor = default;

      if (mesh == null) continue;

      var vertices = new List<Vertex>();
      var indices = new List<uint>();

      foreach (var primitive in mesh.Primitives) {
        // init

        var textureCoords = new List<Vector2>();
        var normals = new List<Vector3>();

        var material = primitive.Material;
        if (material == null) continue;

        // material

        baseColor = material.FindChannel("BaseColor")!.Value;

        // position

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

      if (vertices.Count < 1) {
        continue;
      }

      var meshData = new Mesh {
        Vertices = [.. vertices],
        Indices = [.. indices]
      };

      meshMaterialPair.Add(meshData, baseColor);
    }

    foreach (var skin in modelRoot.LogicalSkins) {

    }
  }

}
