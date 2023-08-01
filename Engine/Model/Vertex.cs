using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using OpenTK.Mathematics;

using Vortice.Vulkan;

namespace Dwarf.Engine;

public struct Vertex {
  public Vector3 Position;
  public Vector3 Color;
  public Vector3 Normal;
  public Vector2 Uv;
}

public struct SimpleVertex {
  public Vector3 Position;
  public Vector3 Color;
}