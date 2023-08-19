﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Numerics;

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
      indices.Add(bottom1);
      indices.Add(top2);


      indices.Add(bottom2);
      indices.Add(bottom1);
      indices.Add(top2);

      indices.Add((uint)vertices.Count() - 1);
      indices.Add(top1);
      indices.Add(top2);


      indices.Add((uint)vertices.Count() - 2);
      indices.Add(bottom1);
      indices.Add(bottom2);
    }

    var mesh = new Mesh();
    mesh.Vertices = vertices.ToArray();
    mesh.Indices = indices.ToArray();
    return mesh;
  }
}