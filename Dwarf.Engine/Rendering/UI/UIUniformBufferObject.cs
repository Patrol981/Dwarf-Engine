using System.Runtime.InteropServices;

namespace Dwarf;

[StructLayout(LayoutKind.Explicit)]
public struct UIUniformObject {
  [FieldOffset(0)] public System.Numerics.Matrix4x4 UIMatrix;
}