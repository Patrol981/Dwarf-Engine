using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct JointShaderData {

  [FieldOffset(0)] public Matrix4x4* Matrices;
  // [FieldOffset(8192)] public Vector4 MatCount;
  // [FieldOffset(192)] public Matrix4x4[] JointMatrices;
  // [FieldOffset(8128)] public Vector3 JointCount;
  // [FieldOffset(128)] public int IsSkinned;
}
