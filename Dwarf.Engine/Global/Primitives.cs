using System.Numerics;

using Dwarf.Extensions.Logging;

namespace Dwarf.Engine;

public enum PrimitiveType {
  Cylinder,
  Box,
  Capsule,
  Convex,
  Sphere,
  Torus,
  None
}

public static class Primitives {
  static Vector3 Color = new(1f, 1f, 1f);

  public static Mesh CreatePrimitive(PrimitiveType primitiveType) {
    var mesh = new Mesh();

    switch (primitiveType) {
      case PrimitiveType.Cylinder:
        mesh = CreateCylinderPrimitive(2, 2, 20);
        break;
      case PrimitiveType.Box:
        mesh = CreateBoxPrimitive(1);
        break;
      case PrimitiveType.Sphere:
        mesh = CreateSpherePrimitve(128, 128);
        break;
      default:
        break;
    }

    return mesh;
  }

  public static Mesh CreateConvex(Mesh inputMesh) {
    return inputMesh;
  }

  public static Mesh CreateConvex(Mesh[] meshes, bool flip = false) {
    Logger.Info($"len: {meshes.Length}");

    var outputMesh = new Mesh();
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    uint vertexOffset = 0;

    foreach (var m in meshes) {
      for (int vertexIndex = 0; vertexIndex < m.Vertices.Length; vertexIndex++) {
        var vertex = m.Vertices[vertexIndex];
        Vector3 updatePos = flip ? new(vertex.Position.X, -vertex.Position.Y, vertex.Position.Z) : vertex.Position;

        vertex.Position = updatePos;
        vertices.Add(vertex);
      }

      foreach (var index in m.Indices) {
        indices.Add(index + vertexOffset);
      }

      vertexOffset += (uint)m.Vertices.Length;
    }

    outputMesh.Vertices = vertices.ToArray();
    outputMesh.Indices = indices.ToArray();

    Logger.Info($"vert[0]: {meshes[0].Vertices.Length}");
    Logger.Info($"out vert: {outputMesh.Vertices.Length}");

    // return meshes[1];
    return outputMesh;
  }

  public static Mesh CreateBoxPrimitive(float scale) {
    Vector3[] normals = [
      Vector3.UnitZ,
      -Vector3.UnitZ,
      Vector3.UnitZ,
      Vector3.UnitX,
      Vector3.UnitX,
      Vector3.UnitY,
      -Vector3.UnitY,
      Vector3.UnitZ
    ];

    Vertex[] vertices = [
      new Vertex { Position = new Vector3(-scale, -scale, -scale), Color = Color, Normal = normals[0], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, -scale, -scale), Color = Color, Normal = normals[1], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, scale, -scale), Color = Color, Normal = normals[2], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(-scale, scale, -scale), Color = Color, Normal = normals[3], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(-scale, -scale, scale), Color = Color, Normal = normals[4], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, -scale, scale), Color = Color, Normal = normals[5], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, scale, scale), Color = Color, Normal = normals[6], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(-scale, scale, scale), Color = Color, Normal = normals[7], Uv = Vector2.Zero }
    ];

    uint[] indices = [
      // Front face
      0,
      3,
      2,
      2,
      1,
      0,

      // Back face
      4,
      5,
      6,
      6,
      7,
      4,

      // Left face
      0,
      4,
      7,
      7,
      3,
      0,

      // Right face
      1,
      2,
      6,
      6,
      5,
      1,

      // Top face
      3,
      7,
      6,
      6,
      2,
      3,

      // Bottom face
      0,
      1,
      5,
      5,
      4,
      0
    ];

    var mesh = new Mesh {
      Indices = indices,
      Vertices = vertices
    };
    return mesh;
  }

  public static Mesh CreateCylinderPrimitive(float radius = 0.5f, float height = 1.0f, int segments = 20) {
    // height = height * -1;
    float cylinderStep = (MathF.PI * 2) / segments;
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    // z x y

    for (int i = 0; i < segments; ++i) {
      float theta = cylinderStep * i;
      float x = radius * (float)MathF.Cos(theta);
      float z = radius * (float)MathF.Sin(theta);

      // var pos = new Vector3(x, 0.0f, z);
      var y = 0.0f;
      var pos = new Vector3(x, y, z);
      var normal = Vector3.Normalize(new Vector3(x, y, z));
      var vertex = new Vertex();

      vertex.Position = pos;
      vertex.Normal = normal;
      vertex.Color = Color;
      vertices.Add(vertex);

      pos.Y = -height;
      normal = -normal;
      vertex.Position = pos;
      vertex.Normal = normal;
      vertices.Add(vertex);
    }

    var topCenter = new Vector3(0.0f, -height, 0.0f);
    // var topCenter = new Vector3(0.0f, 0.0f, height);
    var bottomCenter = new Vector3(0.0f, 0.0f, 0.0f);

    var vertexTop = new Vertex();
    vertexTop.Position = topCenter;
    vertexTop.Normal = new(0.0f, 1.0f, 0.0f);
    vertices.Add(vertexTop);

    vertexTop.Position = bottomCenter;
    vertexTop.Normal = new(0.0f, -1.0f, 0.0f);
    vertices.Add(vertexTop);

    for (int i = 0; i < segments; ++i) {
      uint top1 = (uint)(2 * i);
      uint top2 = ((uint)((i + 1) % segments) * 2);
      uint bottom1 = top1 + 1;
      uint bottom2 = top2 + 1;

      indices.Add(top1);
      // indices.Add(bottom1);
      indices.Add(top2);
      indices.Add(bottom1);


      indices.Add(bottom2);
      // indices.Add(bottom1);
      indices.Add(bottom1);
      indices.Add(top2);

      indices.Add((uint)vertices.Count() - 1);
      indices.Add(top2);
      // indices.Add(bottom1);
      indices.Add(top1);

      indices.Add((uint)vertices.Count() - 2);
      indices.Add(bottom1);
      // indices.Add(top2);
      indices.Add(bottom2);
    }

    var mesh = new Mesh();
    mesh.Vertices = vertices.ToArray();
    mesh.Indices = indices.ToArray();
    return mesh;
  }

  public static Mesh CreateSpherePrimitve(int slices, int stacks) {
    Mesh mesh = new();
    // List<Vertex> vertices = new();
    var vertices = new Vertex[slices * stacks];
    int index = 0;

    // top vertex
    /*
    vertices.Add(new() {
      Position = new(0, 1, 0),
      Normal = new(1, 1, 1)
    });
    */

    // generate vertices per stack / slice
    for (int i = 0; i < slices; i++) {
      for (int j = 0; j < stacks - 1; j++) {
        var x = MathF.Sin(MathF.PI * i / slices) * MathF.Cos(2 * MathF.PI * j / stacks);
        var y = MathF.Sin(MathF.PI * i / slices) * MathF.Sin(2 * MathF.PI * j / stacks);
        var z = MathF.Cos(MathF.PI * i / slices);
        vertices[index++] = (new() {
          Position = new(x, y, z),
          Normal = new(0, -1, 0),
          Color = new(1, 1, 1)
        });
      }
    }

    /*
    for (int i = 0; i < slices - 1; i++) {
      var phi = MathF.PI * (i + 1) / stacks;
      for (int j = 0; j < slices; j++) {
        var theta = 2.0f * MathF.PI * j / slices;
        var x = MathF.Sin(phi) * MathF.Cos(theta);
        var y = MathF.Cos(phi);
        var z = MathF.Sin(phi) * MathF.Sin(theta);
        vertices.Add(new() {
          Position = new(x, y, z),
          Normal = new(1, 1, 1)
        });
      }
    }
    */

    // bottom vertex
    /*
    vertices.Add(new() {
      Position = new(0, -1, 0),
      Normal = new(1, 1, 1)
    });
    */

    /*
    // top and bottom triangles
    for (int i = 0; i < slices; ++i) {
      var i0 = i + 1;
      var i1 = (i + 1) % slices + 1;
      vertices.AddRange([
        new() {
          Position = new(0, 1, 0),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i1, i1, i1),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i0, i0, i0),
          Normal = new(1,1,1)
        },
      ]);

      i0 = i + slices * (stacks - 2) + 1;
      i1 = (i + 1) % slices + slices * (stacks - 2) + 1;
      vertices.AddRange([
        new() {
          Position = new(0, -1, 0),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i0, i0, i0),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i1, i1, i1),
          Normal = new(1,1,1)
        },
      ]);
    }

    // quads per stack
    for (int j = 0; j < stacks - 2; j++) {
      var j0 = j * slices + 1;
      var j1 = (j + 1) * slices + 1;
      for (int i = 0; i < slices; i++) {
        var i0 = j0 + i;
        var i1 = j0 + (i + 1) % slices;
        var i2 = j1 + (i + 1) % slices;
        var i3 = j1 + i;
        vertices.AddRange([
          new() {
            Position = new(i0, i0, i0),
            Normal = new(1,1,1)
          },
          new() {
            Position = new(i1, i1, i1),
            Normal = new(1,1,1)
          },
          new() {
            Position = new(i2, i2, i2),
            Normal = new(1,1,1)
          },
          new() {
            Position = new(i3, i3, i3),
            Normal = new(1,1,1)
          }
        ]);
      }
    }
    */

    mesh.Vertices = [.. vertices];
    return mesh;
  }
}
