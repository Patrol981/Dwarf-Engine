
using System.Runtime.InteropServices;

using OpenTK.Mathematics;

namespace Dwarf.Engine.Physics;

[StructLayout(LayoutKind.Explicit)]
public struct ColliderMeshPushConstant {
  [FieldOffset(0)] public Matrix4 ModelMatrix;
}