using System.Runtime.InteropServices;

using System.Numerics;

namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct ModelUniformBufferObject {
  [FieldOffset(0)] public MaterialData Material;
}