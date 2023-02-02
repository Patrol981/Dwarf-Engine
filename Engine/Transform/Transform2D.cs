using Dwarf.Engine.EntityComponentSystem;
using OpenTK.Mathematics;

namespace Dwarf.Engine;

public class Transform2D : Component {
  public Vector2 Translation;
  public Vector3 Scale = new(1, 1, 1);
  public float Rotation = 0.0f;
  public Transform2D() { }

  public Transform2D(Vector2 translation) {
    Translation = translation;
  }

  public Transform2D(Vector2 translation, Vector3 scale) {
    Translation = translation;
    Scale = scale;
  }

  private Matrix4 GetMatrix() {
    var s = MathF.Sin(Rotation);
    var c = MathF.Cos(Rotation);

    var rotationMatrix = new Matrix4();
    rotationMatrix[0, 0] = c;
    rotationMatrix[0, 1] = s;
    rotationMatrix[1, 0] = -s;
    rotationMatrix[1, 1] = c;

    return rotationMatrix * Matrix4.CreateScale(Scale);
  }

  public Matrix4 Matrix4 => GetMatrix();
}