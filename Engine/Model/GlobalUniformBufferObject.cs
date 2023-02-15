using OpenTK.Mathematics;

namespace Dwarf.Engine;
public struct GlobalUniformBufferObject {
  // public Matrix4 ModelMatrix;
  public Matrix4 View;
  public Matrix4 Projection;
  public Vector3 LightDirection;
}