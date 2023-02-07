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