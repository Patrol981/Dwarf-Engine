using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;

using System.Numerics;

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
    Position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    Rotation = new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation) {
    Position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    Rotation = rotation != Vector3.Zero ? rotation : new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation, Vector3 scale) {
    Position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    Rotation = rotation != Vector3.Zero ? rotation : new Vector3(0, 0, 0);
    Scale = scale != Vector3.Zero ? scale : new Vector3(1, 1, 1);
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

  private Matrix4x4 GetMatrix() {
    var modelPos = Position;
    var angleX = Converter.DegreesToRadians(Rotation.X);
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var angleZ = Converter.DegreesToRadians(Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    var worldModel = Matrix4x4.CreateScale(Scale) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  public OpenTK.Mathematics.Matrix4 GetMatrix2D() {

    var c3 = (float)MathF.Cos(Rotation.Z);
    var s3 = (float)MathF.Sin(Rotation.Z);
    var c2 = (float)MathF.Cos(Rotation.X);
    var s2 = (float)MathF.Sin(Rotation.Y);
    var c1 = (float)MathF.Cos(Rotation.Y);
    var s1 = (float)MathF.Sin(Rotation.Y);

    var mat = OpenTK.Mathematics.Matrix4.Zero;
    mat[0, 0] = Scale.X * (c1 * c3 + s1 * s2 * s3);
    mat[0, 1] = Scale.X * (c2 * s3);
    mat[0, 2] = Scale.X * (c1 * s2 * s3 - c3 * s1);

    mat[1, 0] = Scale.Y * (c3 * s1 * s2 - c1 * s3);
    mat[1, 1] = Scale.Y * (c2 * c3);
    mat[1, 2] = Scale.Y * (c1 * c3 * s2 + s1 * s3);

    mat[2, 0] = Scale.Z * (c2 * s1);
    mat[2, 1] = Scale.Z * (-s2);
    mat[2, 2] = Scale.Z * (c1 * c2);

    mat[3, 0] = Position.X;
    mat[3, 1] = Position.Y;
    mat[3, 2] = Position.Z;
    mat[3, 3] = 1.0f;

    return mat;
  }

  private Matrix4x4 GetMatrixWithoutRotation() {
    var modelPos = Position;
    var worldModel = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetNormalMatrix() {
    var angleX = Converter.DegreesToRadians(Rotation.X);
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var angleZ = Converter.DegreesToRadians(Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    rotation *= Matrix4x4.CreateScale(Scale);
    return rotation;
  }

  public System.Numerics.Matrix4x4 Matrix4 => GetMatrix();
  public Matrix4x4 MatrixWithoutRotation => GetMatrixWithoutRotation();
  public Matrix4x4 NormalMatrix => GetNormalMatrix();

}