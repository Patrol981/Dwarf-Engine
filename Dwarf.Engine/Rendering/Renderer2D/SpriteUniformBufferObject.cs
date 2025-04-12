using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf.Rendering.Renderer2D;

[StructLayout(LayoutKind.Explicit)]
public struct SpriteUniformBufferObject {
  [FieldOffset(0)] public Matrix4x4 SpriteMatrix;
  [FieldOffset(64)] public Vector3 SpriteColor;
  [FieldOffset(76)] public bool UseTexture;
}