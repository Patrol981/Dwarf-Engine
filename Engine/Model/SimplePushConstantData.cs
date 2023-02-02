using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.Engine.Math;
using OpenTK.Mathematics;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct SimplePushConstantData {
  [FieldOffset(0)] public Matrix4 Transform;
  [FieldOffset(64)] public Vector3 Color;
}