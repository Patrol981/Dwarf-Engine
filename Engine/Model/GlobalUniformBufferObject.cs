using OpenTK.Mathematics;

namespace Dwarf.Engine;
public struct GlobalUniformBufferObject {
  public Matrix4 Model;
  public Matrix4 View;
  public Matrix4 Projection;
  public Vector3 LightDirection;
}