using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct GlobalUniformBufferObject {
  [FieldOffset(0)] public Matrix4 View;
  [FieldOffset(64)] public Matrix4 Projection;
  [FieldOffset(128)] public Vector3 LightPosition;
  [FieldOffset(140)] public Vector4 LightColor;
  [FieldOffset(156)] public Vector4 AmientLightColor;
}