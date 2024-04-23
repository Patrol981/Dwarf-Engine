using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf;

[StructLayout(LayoutKind.Explicit)]
public struct SimplePushConstantData {
  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
  [FieldOffset(64)] public Matrix4x4 NormalMatrix;
}