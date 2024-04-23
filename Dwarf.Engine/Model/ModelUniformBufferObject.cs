using System.Runtime.InteropServices;

namespace Dwarf;

[StructLayout(LayoutKind.Explicit)]
public struct ModelUniformBufferObject {
  [FieldOffset(0)] public MaterialData Material;
}