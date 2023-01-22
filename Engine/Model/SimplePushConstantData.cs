using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.Engine.Math;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct SimplePushConstantData {
  [FieldOffset(0)] public Matrix4x4 Transform;
  [FieldOffset(64)] public Vector2 Offset;
  [FieldOffset(16 + 64)] public Vector3 Color;
}