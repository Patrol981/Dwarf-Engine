using System.Runtime.InteropServices;
using System.Numerics;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct SkyboxBufferObject {
  [FieldOffset(0)] public Matrix4x4 SkyboxMatrix;
  [FieldOffset(64)] public Vector3 SkyboxColor;
}