using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK.Mathematics;

namespace Dwarf.Engine;

public enum PrimitiveType {
  Cylinder,
  Box,
  Capsule,
  Convex,
  Sphere,
  None
}

public static class Primitives {
  static Vector3 Color = new Vector3(0.19f, 0.65f, 0.32f);

  public static Mesh CreateCylinderPrimitive(float radius = 0.5f, float height = 1.0f, int segments = 20) {
    float cylinderStep = MathHelper.TwoPi / segments;
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    // z x y

    for (int i = 0; i < segments; ++i) {
      float theta = cylinderStep * i;
      float x = radius * (float)MathHelper.Cos(theta);
      float z = radius * (float)MathHelper.Sin(theta);

      // var pos = new Vector3(x, 0.0f, z);
      var y = 0.0f;
      var pos = new Vector3(z, x, y);
      var normal = Vector3.Normalize(new Vector3(z, x, y));
      var vertex = new Vertex();

      vertex.Position = pos;
      vertex.Normal = normal;
      vertex.Color = Color;
      vertices.Add(vertex);

      pos.Z = height;
      normal = -normal;
      vertex.Position = pos;
      vertex.Normal = normal;
      vertices.Add(vertex);
    }

    // var topCenter = new Vector3(0.0f, height, 0.0f);
    var topCenter = new Vector3(0.0f, 0.0f, height);
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
      indices.Add(bottom1);
      indices.Add(top2);

      indices.Add(top2);
      indices.Add(bottom1);
      indices.Add(bottom2);

      indices.Add((uint)vertices.Count() - 1);
      indices.Add(top1);
      indices.Add(top2);

      indices.Add((uint)vertices.Count() - 2);
      indices.Add(bottom2);
      indices.Add(bottom1);
    }

    var mesh = new Mesh();
    mesh.Vertices = vertices.ToArray();
    mesh.Indices = indices.ToArray();
    return mesh;
  }

  public static Mesh CreateCapsulePrimitive(float radius = 0.5f, float height = 1.0f, int segments = 20) {
    float halfSphereStep = MathHelper.Pi / (segments / 2);

    var vertices = new List<Vertex>();

    // Create half-sphere vertices at the top
    for (int i = 0; i <= segments / 4; ++i) {
      float phi = halfSphereStep * i;
      float z = height * 0.5f - radius * (float)MathHelper.Cos(phi);
      float rad = radius * (float)MathHelper.Sin(phi);
      for (int j = 0; j < segments; ++j) {
        float theta = MathHelper.TwoPi / segments * j;
        float x = rad * (float)MathHelper.Cos(theta);
        float y = rad * (float)MathHelper.Sin(theta);
        var pos = new Vector3(x, y, z);
        var normal = Vector3.Normalize(pos);
        var vertex = new Vertex();
        vertex.Position = pos;
        vertex.Normal = normal;
        vertices.Add(vertex);
      }
    }

    // Create cylinder vertices
    for (int i = 0; i < segments / 2; ++i) {
      float z = -height * 0.5f + i / ((segments / 2) - 1) * height;
      for (int j = 0; j < segments; ++j) {
        float theta = MathHelper.TwoPi / segments * j;
        float x = radius * (float)MathHelper.Cos(theta);
        float y = radius * (float)MathHelper.Sin(theta);
        var pos = new Vector3(x, y, z);
        var normal = Vector3.Normalize(new Vector3(x, 0.0f, z));
        var vertex = new Vertex();
        vertex.Position = pos;
        vertex.Normal = normal;
        vertex.Color = new(1, 1, 1);
        vertices.Add(vertex);
      }
    }

    // Create half-sphere vertices at the bottom
    for (int i = segments / 4; i <= segments / 2; ++i) {
      float phi = halfSphereStep * i;
      float z = -height * 0.5f + radius * MathF.Cos(phi);
      float rad = radius * (float)MathHelper.Sin(phi);
      for (int j = 0; j < segments; ++j) {
        float theta = MathHelper.TwoPi / (segments * j);
        float x = rad * (float)MathHelper.Cos(theta);
        float y = rad * (float)MathHelper.Sin(theta);
        var pos = new Vector3(x, y, z);
        var normal = Vector3.Normalize(pos);
        var vertex = new Vertex();
        vertex.Position = pos;
        vertex.Normal = normal;
        vertices.Add(vertex);
      }
    }

    var mesh = new Mesh();
    mesh.Vertices = vertices.ToArray();
    return mesh;
  }
}
