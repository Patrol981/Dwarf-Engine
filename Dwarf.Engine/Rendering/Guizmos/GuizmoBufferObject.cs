using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf.Engine.Rendering;

[StructLayout(LayoutKind.Explicit)]
public struct GuizmoBufferObject {
  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
  [FieldOffset(64)] public int GuizmoType;
}

