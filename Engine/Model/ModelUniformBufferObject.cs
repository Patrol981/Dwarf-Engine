using System.Runtime.InteropServices;
using OpenTK.Mathematics;
namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct ModelUniformBufferObject {
  [FieldOffset(0)] public Matrix4 ModelMatrix;
  [FieldOffset(64)] public Matrix4 NormalMatrix;
  [FieldOffset(128)] public Vector3 Material;
}