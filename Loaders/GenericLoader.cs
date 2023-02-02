using Assimp;
using Dwarf.Engine;
using Dwarf.Vulkan;
using OpenTK.Mathematics;

namespace Dwarf.Engine.Loaders;

public class GenericLoader {
  public Model LoadModel(Device device, string path) {
    var assimpContext = new AssimpContext();

    var scene = assimpContext.ImportFile($"{path}",
      PostProcessSteps.Triangulate |
      PostProcessSteps.GenerateSmoothNormals |
      PostProcessSteps.FlipUVs |
      PostProcessSteps.CalculateTangentSpace
    );

    var node = scene.RootNode;
    var mesh = new List<Vertex>();

    // if (!node.HasChildren) return mesh.ToArray();
    foreach (var child in node.Children) {
      foreach (int index in child.MeshIndices) {
        List<Vector3> posList = new();
        List<Color4> colorList = new();
        List<Vector3> texList = new();
        List<Vector3> normalList = new();
        List<int> indices = new();

        var aMesh = scene.Meshes[index];
        var vertexArray = new List<Vertex>();

        foreach (Face face in aMesh.Faces) {
          for (int i = 0; i < face.IndexCount; i++) {
            int indice = face.Indices[i];

            indices.Add(indice);
            var vertex = new Vertex();

            bool hasColors = aMesh.HasVertexColors(0);
            bool hasTexCoords = aMesh.HasTextureCoords(0);

            Vector3 pos = FromVector(aMesh.Vertices[indice]);
            //posList.Add(pos);
            vertex.Position = pos;
            vertex.Color = new Vector3(0.2f, 0.5f, 0.9f);

            vertexArray.Add(vertex);
          }
        }

        mesh.AddRange(vertexArray);
      }
    }


    assimpContext.Dispose();
    return new Model(device, mesh.ToArray());

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
}