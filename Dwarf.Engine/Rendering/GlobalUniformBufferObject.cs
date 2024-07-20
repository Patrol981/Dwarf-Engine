using System.Numerics;
using System.Runtime.InteropServices;

using Dwarf.Rendering.Lightning;

namespace Dwarf.Rendering;

[StructLayout(LayoutKind.Explicit)]
public struct GlobalUniformBufferObject {
  // std 430
  [FieldOffset(0)] public Matrix4x4 View;
  [FieldOffset(64)] public Matrix4x4 Projection;
  [FieldOffset(128)] public Vector3 CameraPosition;
  [FieldOffset(140)] public int Layer;
  [FieldOffset(144)] public DirectionalLight DirectionalLight; // 48
  [FieldOffset(192)] public int PointLightsLength;
  // [FieldOffset(208)] public PointLight[] PointLights;
  // [FieldOffset(192)] public unsafe PointLight* PointLights;
  // [FieldOffset(4288)] public int PointLightsLength;


  // [FieldOffset(208)] public unsafe PointLight* PointLights;

  // std140
  /*
  [FieldOffset(0)] public Matrix4x4 View;
  [FieldOffset(64)] public Matrix4x4 Projection;
  [FieldOffset(128)] public Vector3 CameraPosition;
  [FieldOffset(144)] public int Layer;
  [FieldOffset(160)] public DirectionalLight DirectionalLight; // 48
  [FieldOffset(208)] public int PointLightsLength;
  [FieldOffset(224)] public PointLight[] PointLights;
  */
}