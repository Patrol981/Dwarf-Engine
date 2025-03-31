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
  [FieldOffset(140)] public float Fov;
  [FieldOffset(144)] public DirectionalLight DirectionalLight; // 48
  [FieldOffset(192)] public int PointLightsLength;
  [FieldOffset(196)] public int HasImportantEntity;
  [FieldOffset(208)] public Vector3 ImportantEntityPosition;
  [FieldOffset(224)] public Vector3 ImportantEntityDirection;
  [FieldOffset(240)] public Vector3 Fog; // X,Y for coords Z for alpha
  [FieldOffset(252)] public int UseFog;
  [FieldOffset(256)] public Vector4 FogColor;
  [FieldOffset(272)] public Vector2 ScreenSize;
  [FieldOffset(280)] public float HatchScale;
  // [FieldOffset(284)] public Vector3 Luminance;

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