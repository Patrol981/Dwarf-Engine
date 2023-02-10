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

  private Matrix4 GetMatrix() {
    var modelPos = Position;
    var angleX = MathHelper.DegreesToRadians(Rotation.X);
    var angleY = MathHelper.DegreesToRadians(Rotation.Y);
    var angleZ = MathHelper.DegreesToRadians(Rotation.Z);

    var rotation = Matrix4.CreateRotationX(angleX) * Matrix4.CreateRotationY(angleY) * Matrix4.CreateRotationZ(angleZ);
    var worldModel = rotation * Matrix4.CreateTranslation(modelPos);
    worldModel *= Matrix4.CreateScale(Scale);
    return worldModel;
  }

  private Matrix4 GetNormalMatrix() {
    var angleX = MathHelper.DegreesToRadians(Rotation.X);
    var angleY = MathHelper.DegreesToRadians(Rotation.Y);
    var angleZ = MathHelper.DegreesToRadians(Rotation.Z);
    var rotation = Matrix4.CreateRotationX(angleX) * Matrix4.CreateRotationY(angleY) * Matrix4.CreateRotationZ(angleZ);
    rotation *= Matrix4.CreateScale(Scale);
    return rotation;
  }

  public Matrix4 Matrix4 => GetMatrix();
  public Matrix4 NormalMatrix => GetNormalMatrix();

}