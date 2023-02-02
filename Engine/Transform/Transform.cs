using Dwarf.Engine.EntityComponentSystem;
using OpenTK.Mathematics;

namespace Dwarf.Engine;

public class Transform : Component {
  public Vector3 Position;
  public Vector3 Rotation;
  public Vector3 Scale;

  public Transform() {
    Position = new Vector3(0, 0, 0);
    Rotation = new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position) {
    Position = position != null ? position : new Vector3(0, 0, 0);
    Rotation = new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation) {
    Position = position != null ? position : new Vector3(0, 0, 0);
    Rotation = rotation != null ? rotation : new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation, Vector3 scale) {
    Position = position != null ? position : new Vector3(0, 0, 0);
    Rotation = rotation != null ? rotation : new Vector3(0, 0, 0);
    Scale = scale != null ? scale : new Vector3(1, 1, 1);
  }

  public void IncreasePosition(Vector3 position) {
    Position.X += position.X;
    Position.Y += position.Y;
    Position.Z += position.Z;
  }

  public void IncreaseRotation(Vector3 rotation) {
    Rotation.X += rotation.X;
    Rotation.Y += rotation.Y;
    Rotation.Z += rotation.Z;
  }

  private Matrix4 GetMatrix() {
    // var transform = Matrix4.Identity;

    var modelPos = Position;
    var angleX = MathHelper.DegreesToRadians(Rotation.X);
    var angleY = MathHelper.DegreesToRadians(Rotation.Y);
    var angleZ = MathHelper.DegreesToRadians(Rotation.Z);

    var rotation = Matrix4.CreateRotationX(angleX) * Matrix4.CreateRotationY(angleY) * Matrix4.CreateRotationZ(angleZ);
    var worldModel = rotation * Matrix4.CreateTranslation(modelPos);
    //worldModel *= Matrix4.CreateRotationX(MathHelper.DegreesToRadians(Rotation.X));
    //worldModel *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(Rotation.Y));
    //worldModel *= Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Rotation.Z));
    worldModel *= Matrix4.CreateScale(Scale);
    // worldModel *= Matrix4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4 GetCustomMatrix4() {
    float c3 = MathF.Cos(Rotation.Z);
    float s3 = MathF.Sin(Rotation.Z);
    float c2 = MathF.Cos(Rotation.X);
    float s2 = MathF.Sin(Rotation.X);
    float c1 = MathF.Cos(Rotation.Y);
    float s1 = MathF.Sin(Rotation.Y);

    var mat = new Matrix4();

    mat[0, 0] = Scale.X * (c1 * c3 + s1 * s2 * s3);
    mat[1, 0] = Scale.X * (c2 * s3);
    mat[2, 0] = Scale.X * (c1 * s2 * s3 - c3 * s1);
    mat[3, 0] = 0.0f;

    mat[0, 1] = Scale.Y * (c3 * s1 * s2 - c1 * s3);
    mat[1, 1] = Scale.Y * (c2 * c3);
    mat[2, 1] = Scale.Y * (c1 * c3 * s2 + s1 * s3);
    mat[3, 1] = 0.0f;

    mat[0, 2] = Scale.Z * (c2 * s1);
    mat[1, 2] = Scale.Z * (-s2);
    mat[2, 2] = Scale.Z * (c1 * c2);
    mat[3, 2] = 0.0f;

    mat[0, 3] = Position.X;
    mat[1, 3] = Position.Y;
    mat[2, 3] = Position.Z;
    mat[3, 3] = 1.0f;

    return mat;

    return new Matrix4(
    Scale.X * (c1 * c3 + s1 * s2 * s3),
    Scale.X * (c2 * s3),
    Scale.X * (c1 * s2 * s3 - c3 * s1),
    0.0f,
    Scale.Y * (c3 * s1 * s2 - c1 * s3),
    Scale.Y * (c2 * c3),
    Scale.Y * (c1 * c3 * s2 + s1 * s3),
    0.0f,
    Scale.Z * (c2 * s1),
    Scale.Z * (-s2),
    Scale.Z * (c1 * c2),
    0.0f,
    Position.X, Position.Y, Position.Z, 1.0f);
  }

  public Matrix4 Matrix4 => GetMatrix();

}