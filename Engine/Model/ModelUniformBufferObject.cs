using OpenTK.Mathematics;

namespace Dwarf.Engine;

public struct ModelUniformBufferObject {
  public Matrix4 ModelMatrix;
  public Matrix4 NormalMatrix;
  public Vector3 Material;
}