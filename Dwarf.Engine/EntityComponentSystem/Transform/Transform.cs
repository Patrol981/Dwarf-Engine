using System.Numerics;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;

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

    if (Rotation.X > 360) {
      var offset = Rotation.X - 360;
      Rotation.X = 0 + offset;
    }

    if (Rotation.Y > 360) {
      var offset = Rotation.Y - 360;
      Rotation.Y = 0 + offset;
    }

    if (Rotation.Z > 360) {
      var offset = Rotation.Z - 360;
      Rotation.Z = 0 + offset;
    }
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

  private Matrix4x4 GetRotation() {
    var angleX = Converter.DegreesToRadians(Rotation.X);
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var angleZ = Converter.DegreesToRadians(Rotation.Z);
    return Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
  }

  private Matrix4x4 GetScale() {
    return Matrix4x4.CreateScale(Scale);
  }

  private Matrix4x4 GetPosition() {
    var modelPos = Position;
    return Matrix4x4.CreateTranslation(modelPos);
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

  private Vector3 GetForward() {
    var modelMatrix = Matrix4;
    var forward = new Vector3(modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
    forward = Vector3.Normalize(forward);
    return forward;
  }

  public Vector3 Forward => GetForward();
  public Matrix4x4 Matrix4 => GetMatrix();
  public Matrix4x4 ScaleMatrix => GetScale();
  public Matrix4x4 RotationMatrix => GetRotation();
  public Matrix4x4 PositionMatrix => GetPosition();
  public Matrix4x4 MatrixWithoutRotation => GetMatrixWithoutRotation();
  public Matrix4x4 NormalMatrix => GetNormalMatrix();

}