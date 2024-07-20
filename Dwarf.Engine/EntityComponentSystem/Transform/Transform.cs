using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Math;

namespace Dwarf;

public class Transform : Component {
  private Vector3 _position;
  private Vector3 _rotation;
  private Vector3 _scale;

  public Transform() {
    _position = new Vector3(0, 0, 0);
    _rotation = new Vector3(0, 0, 0);
    _scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position) {
    _position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    _rotation = new Vector3(0, 0, 0);
    _scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation) {
    _position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    _rotation = rotation != Vector3.Zero ? rotation : new Vector3(0, 0, 0);
    _scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation, Vector3 scale) {
    _position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    _rotation = rotation != Vector3.Zero ? rotation : new Vector3(0, 0, 0);
    _scale = scale != Vector3.Zero ? scale : new Vector3(1, 1, 1);
  }

  public void IncreasePosition(Vector3 position) {
    _position.X += position.X;
    _position.Y += position.Y;
    _position.Z += position.Z;
  }

  public void IncreaseRotation(Vector3 rotation) {
    _rotation.X += rotation.X;
    _rotation.Y += rotation.Y;
    _rotation.Z += rotation.Z;

    if (_rotation.X > 360) {
      var offset = _rotation.X - 360;
      _rotation.X = 0 + offset;
    }

    if (_rotation.Y > 360) {
      var offset = _rotation.Y - 360;
      _rotation.Y = 0 + offset;
    }

    if (_rotation.Z > 360) {
      var offset = _rotation.Z - 360;
      _rotation.Z = 0 + offset;
    }
  }

  public void IncreaseRotationX(float value) {
    _rotation.X += value;

    if (_rotation.X > 360) {
      var offset = _rotation.X - 360;
      _rotation.X = 0 + offset;
    }
  }

  public void IncreaseRotationY(float value) {
    _rotation.Y += value;

    if (_rotation.Y > 360) {
      var offset = _rotation.Y - 360;
      _rotation.Y = 0 + offset;
    }
  }

  public void IncreaseRotationZ(float value) {
    _rotation.Z += value;

    if (_rotation.Z > 360) {
      var offset = _rotation.Z - 360;
      _rotation.Z = 0 + offset;
    }
  }

  /// <summary>
  /// Sets Transform euler angle to given position only by Y axis
  /// </summary>
  /// <param name="position"></param>
  public void LookAtFixed(Vector3 position) {
    var direction = position - _position;
    direction.Y = 0;
    direction = Vector3.Normalize(direction);
    var yaw = MathF.Atan2(direction.X, direction.Z);
    yaw = Converter.RadiansToDegrees(yaw);
    _rotation.Y = yaw;
  }

  public void LookAtFixedRound(Vector3 position) {
    LookAtFixed(position);
    _rotation.Y = Clamp.ClampToClosestAngle(_rotation.Y);
  }

  public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) {
    var vec = target - current;
    var mag = Vector3.Distance(current, target);

    if (mag <= maxDistanceDelta || mag == 0.0f) {
      return target;
    }
    return current + Vector3.Normalize(vec) * maxDistanceDelta;
  }

  private Matrix4x4 GetMatrix() {
    var modelPos = _position;
    var angleX = Converter.DegreesToRadians(_rotation.X);
    var angleY = Converter.DegreesToRadians(_rotation.Y);
    var angleZ = Converter.DegreesToRadians(_rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    var worldModel = Matrix4x4.CreateScale(_scale) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetMatrixWithYAngleRotation() {
    var modelPos = _position;
    var angleY = Converter.DegreesToRadians(_rotation.Y);
    var rotation = Matrix4x4.CreateRotationY(angleY);
    var worldModel = Matrix4x4.CreateScale(_scale) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetRotation() {
    var angleX = Converter.DegreesToRadians(_rotation.X);
    var angleY = Converter.DegreesToRadians(_rotation.Y);
    var angleZ = Converter.DegreesToRadians(_rotation.Z);
    return Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
  }

  private Matrix4x4 GetAngleY() {
    var angleY = Converter.DegreesToRadians(_rotation.Y);
    return Matrix4x4.CreateRotationY(angleY);
  }

  private Matrix4x4 GetScale() {
    return Matrix4x4.CreateScale(_scale);
  }

  private Matrix4x4 GetPosition() {
    var modelPos = _position;
    return Matrix4x4.CreateTranslation(modelPos);
  }

  private Matrix4x4 GetMatrixWithoutRotation() {
    var modelPos = _position;
    var worldModel = Matrix4x4.CreateScale(_scale) * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetNormalMatrix() {
    var angleX = Converter.DegreesToRadians(_rotation.X);
    var angleY = Converter.DegreesToRadians(_rotation.Y);
    var angleZ = Converter.DegreesToRadians(_rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    rotation *= Matrix4x4.CreateScale(_scale);
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
  public Matrix4x4 AngleYMatrix => GetAngleY();
  public Matrix4x4 PositionMatrix => GetPosition();
  public Matrix4x4 MatrixWithoutRotation => GetMatrixWithoutRotation();
  public Matrix4x4 MatrixWithAngleYRotation => GetMatrixWithYAngleRotation();
  public Matrix4x4 NormalMatrix => GetNormalMatrix();

  public Vector3 Position {
    get { return _position; }
    set { _position = value; }
  }
  public Vector3 Rotation {
    get { return _rotation; }
    set { _rotation = value; }
  }
  public Vector3 Scale {
    get { return _scale; }
    set { _scale = value; }
  }

}