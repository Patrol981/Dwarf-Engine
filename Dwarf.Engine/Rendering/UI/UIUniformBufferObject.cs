using System.Runtime.InteropServices;

using OpenTK.Mathematics;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct UIUniformObject {
  [FieldOffset(0)] public System.Numerics.Matrix4x4 UIMatrix;
}