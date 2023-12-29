using System.Runtime.InteropServices;

using System.Numerics;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct SimplePushConstantData {
  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
  [FieldOffset(64)] public Matrix4x4 NormalMatrix;
}