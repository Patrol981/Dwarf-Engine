using System.Runtime.InteropServices;

using OpenTK.Mathematics;

using Vortice.Vulkan;
namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct SpriteUniformBufferObject {
  [FieldOffset(0)] public Matrix4 SpriteMatrix;
  [FieldOffset(64)] public Vector3 SpriteColor;
  [FieldOffset(76)] public bool UseTexture;
}