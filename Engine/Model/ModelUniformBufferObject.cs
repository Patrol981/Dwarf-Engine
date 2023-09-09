using System.Runtime.InteropServices;

using System.Numerics;

using Vortice.Vulkan;
namespace Dwarf.Engine;

[StructLayout(LayoutKind.Explicit)]
public struct ModelUniformBufferObject {
  [FieldOffset(0)] public Vector3 Material;
}