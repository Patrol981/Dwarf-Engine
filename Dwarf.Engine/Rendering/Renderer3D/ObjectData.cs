using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf.Rendering.Renderer3D;

[StructLayout(LayoutKind.Explicit)]
public struct ObjectData {

  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
  [FieldOffset(64)] public Matrix4x4 NormalMatrix;
  [FieldOffset(128)] public Matrix4x4 NodeMatrix;
  [FieldOffset(192)] public int FilterFlag;
  [FieldOffset(192 + 16)] public Vector4 JointsBufferOffset;
}
