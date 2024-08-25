using System.Numerics;

using Assimp;

using Dwarf.Rendering;
using Dwarf.Vulkan;

namespace Dwarf.Loaders;

public class GenericLoader {
  private Task<List<Mesh>> ProcessChildNode(Assimp.Scene scene, Assimp.Node childNode) {
    List<Mesh> meshes = new();
    // for (int index = 0; index < childNode.Count; index++) {
    foreach (int index in childNode.MeshIndices) {
      var aMesh = scene.Meshes[index];
      var vertexArray = new List<Vertex>();
      var indices = new List<uint>();

      bool hasColors = aMesh.HasVertexColors(0);
      bool hasTexCoords = aMesh.HasTextureCoords(0);

      for (int i = 0; i < aMesh.Faces.Count; i++) {
        var face = aMesh.Faces[i];

        for (int j = 0; j < face.IndexCount; j++) {
          uint indice = (uint)face.Indices[j];
          indices.Add(indice);

          var vertex = new Vertex();

          if (hasColors) {
            OpenTK.Mathematics.Color4 vertColor = FromColor(aMesh.VertexColorChannels[0][(int)indice]);
            vertex.Color = new Vector3(vertColor.R, vertColor.G, vertColor.B);
          } else {
            vertex.Color = new Vector3(0.99f, 0.99f, 0.99f);
          }

          if (aMesh.HasNormals) {
            Vector3 normal = FromVector(aMesh.Normals[(int)indice]);
            vertex.Normal = normal;
          }

          if (hasTexCoords) {
            Vector3 uvw = FromVector(aMesh.TextureCoordinateChannels[0][(int)indice]);
            uvw.Y = 1.0f - uvw.Y;
            vertex.Uv = new(uvw.X, uvw.Y);
            // vertex.Uv = uvw.Xy;
          }

          vertex.Position = FromVector(aMesh.Vertices[(int)indice]);
          vertexArray.Add(vertex);
        }

      }
      var mesh = new Mesh(Application.Instance.Device);
      mesh.Vertices = vertexArray.ToArray();
      mesh.Indices = indices.ToArray();
      meshes.Add(mesh);
    }

    // return meshes;
    return Task.FromResult(meshes);
  }

  public async Task<MeshRenderer> LoadModelOptimized(VulkanDevice device, Renderer renderer, string path) {
    var processingStart = DateTime.Now;
    var assimpContext = new AssimpContext();

    var scene = assimpContext.ImportFile(path,
        PostProcessSteps.Triangulate |
        PostProcessSteps.GenerateSmoothNormals |
        PostProcessSteps.FlipUVs |
        PostProcessSteps.CalculateTangentSpace
    );

    var processingEnd = DateTime.Now;

    var node = scene.RootNode;
    var meshes = new List<Mesh>();

    var tasks = new List<Task<List<Mesh>>>();



    foreach (var child in node.Children) {
      // var result = await ProcessChildNode(scene, child);
      // meshes.AddRange(result);
      tasks.Add(ProcessChildNode(scene, child));
    }



    // Logger.Info($"[PROCESS MESH TIME]: {(processingEnd - processingStart).TotalMilliseconds}");

    await Task.WhenAll(tasks);
    for (int i = 0; i < tasks.Count; i++) {
      meshes.AddRange(tasks[i].Result);
    }

    assimpContext.Dispose();
    return new MeshRenderer(device, renderer, null!, null!, path);
  }

  public MeshRenderer LoadModel(VulkanDevice device, Renderer renderer, string path) {
    var assimpContext = new AssimpContext();

    var scene = assimpContext.ImportFile($"{path}",
      PostProcessSteps.Triangulate |
      PostProcessSteps.GenerateSmoothNormals |
      PostProcessSteps.FlipUVs |
      PostProcessSteps.CalculateTangentSpace |
      PostProcessSteps.OptimizeMeshes
    );

    var node = scene.RootNode;
    var meshes = new List<Dwarf.Mesh>();

    // if (!node.HasChildren) return mesh.ToArray();
    foreach (var child in node.Children) {
      foreach (int index in child.MeshIndices) {
        List<uint> indices = new();

        var aMesh = scene.Meshes[index];
        var vertexArray = new List<Vertex>();

        foreach (Face face in aMesh.Faces) {
          for (int i = 0; i < face.IndexCount; i++) {
            uint indice = (uint)face.Indices[i];

            indices.Add(indice);
            var vertex = new Vertex();

            bool hasColors = aMesh.HasVertexColors(0);
            bool hasTexCoords = aMesh.HasTextureCoords(0);

            if (hasColors) {
              OpenTK.Mathematics.Color4 vertColor = FromColor(aMesh.VertexColorChannels[0][(int)indice]);
              vertex.Color = new Vector3(vertColor.R, vertColor.G, vertColor.B);
            } else {
              vertex.Color = new Vector3(0.99f, 0.99f, 0.99f);
            }

            if (aMesh.HasNormals) {
              Vector3 normal = FromVector(aMesh.Normals[(int)indice]);
              vertex.Normal = normal;
            }

            if (hasTexCoords) {
              Vector3 uvw = FromVector(aMesh.TextureCoordinateChannels[0][(int)indice]);
              uvw.Y = 1.0f - uvw.Y;
              vertex.Uv = new(uvw.X, uvw.Y);
              // vertex.Uv = uvw.Xy;
            }
            Vector3 pos = FromVector(aMesh.Vertices[(int)indice]);
            vertex.Position = pos;
            // vertex.Color = new Vector3(1f, 1f, 1f);

            vertexArray.Add(vertex);
          }
        }

        var mesh = new Mesh(device);
        mesh.Vertices = vertexArray.ToArray();
        mesh.Indices = indices.ToArray();
        meshes.Add(mesh);

        // mesh.Vertices.AddRange(vertexArray);
        //verts.AddRange(vertexArray);
        //inds.AddRange(indices);
      }
    }


    assimpContext.Dispose();
    //mesh.Vertices = verts.ToArray();
    //mesh.Indices = inds.ToArray();
    return new MeshRenderer(device, renderer, null!, null!);

    //for (int i = 0; i < node.ChildCount; i++) {
    //ProcessNode(node.Children[i], scene, ref mesh);
    //}
  }

  /*
    private static void ProcessNode(Node node, Scene scene, ref List<Vertex> mesh) {
      for (int i = 0; i < node.MeshCount; i++) {
        var aMesh = scene.Meshes[node.MeshIndices[i]];
        mesh.Add(ProcessMesh(aMesh, scene));
      }
    }
  */

  private static List<Vertex> ProcessMesh(Assimp.Mesh mesh, Scene scene) {
    List<Vertex> vertices = new List<Vertex>();
    for (int i = 0; i < mesh.Vertices.Count; i++) {
      Vertex v = new();
      Vector3 vec = new();
      vec.X = mesh.Vertices[i].X;
      vec.Y = mesh.Vertices[i].Y;
      vec.Z = mesh.Vertices[i].Z;
      v.Position = vec;
      v.Color = new Vector3(1, 1, 1);

      vertices.Add(v);
    }

    return vertices;
  }

  private static Vector3 FromVector(Vector3D vec) {
    Vector3 v;
    v.X = vec.X;
    v.Y = vec.Y;
    v.Z = vec.Z;
    return v;
  }

  private OpenTK.Mathematics.Color4 FromColor(Color4D color) {
    OpenTK.Mathematics.Color4 c;
    c.R = color.R;
    c.G = color.G;
    c.B = color.B;
    c.A = color.A;
    return c;
  }
}