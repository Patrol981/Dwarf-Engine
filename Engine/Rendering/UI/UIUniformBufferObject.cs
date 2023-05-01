using System.Runtime.InteropServices;

using OpenTK.Mathematics;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct UIUniformObject {
  [FieldOffset(0)] public Vector3 UIColor;
  [FieldOffset(12)] public Matrix4 UIMatrix;
}