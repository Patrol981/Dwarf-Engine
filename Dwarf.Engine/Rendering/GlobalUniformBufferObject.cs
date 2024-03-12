using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf.Engine.Rendering;

[StructLayout(LayoutKind.Explicit)]
// [StructLayout(LayoutKind.Sequential)]
public struct GlobalUniformBufferObject {
  /*
  public System.Numerics.Matrix4x4 View;
  public System.Numerics.Matrix4x4 Projection;
  public Vector3 LightPosition;
  public Vector4 LightColor;
  public Vector4 AmientLightColor;
  public Vector3 CameraPosition;
  */
  [FieldOffset(0)] public System.Numerics.Matrix4x4 View;
  [FieldOffset(64)] public System.Numerics.Matrix4x4 Projection;
  [FieldOffset(128)] public Vector3 LightPosition;
  [FieldOffset(144)] public Vector4 LightColor;
  [FieldOffset(160)] public Vector4 AmientLightColor;
  [FieldOffset(176)] public Vector3 CameraPosition;
  [FieldOffset(188)] public int Layer;
}