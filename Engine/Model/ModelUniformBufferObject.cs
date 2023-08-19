using System.Runtime.InteropServices;

using System.Numerics;

using Vortice.Vulkan;
namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct ModelUniformBufferObject {
  // [FieldOffset(0)] public Matrix4 ModelMatrix;
  // [FieldOffset(64)] public Matrix4 NormalMatrix;
  [FieldOffset(0)] public Vector3 Material;
  [FieldOffset(12)] public bool UseTexture;
  [FieldOffset(16)] public bool UseLight;
}