using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf;

[StructLayout(LayoutKind.Explicit)]
public struct ObjectData {

  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
  [FieldOffset(64)] public Matrix4x4 NormalMatrix;
  [FieldOffset(128)] public Matrix4x4 NodeMatrix;
  // [FieldOffset(128)] public int IsSkinned;
}
