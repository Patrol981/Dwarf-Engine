using System.Security.Cryptography;
using OpenTK.Mathematics;

namespace Dwarf.Engine;

public class Camera {
  private Matrix4 _projectionMatrix = Matrix4.Identity;
  private Matrix4 _viewMatrix = Matrix4.Identity;

  private readonly Vector3 _localUp = new Vector3(0, -1, 0);

  protected Vector3 _front = Vector3.UnitZ;

  protected Vector3 _up = -Vector3.UnitY;

  protected Vector3 _right = Vector3.UnitX;

  public void SetOrthograpicProjection(float left, float right, float top, float bottom, float near, float far) {
    _projectionMatrix = Matrix4.Identity;
    _projectionMatrix[0, 0] = 2f / (right - left);
    _projectionMatrix[1, 1] = 2f / (bottom - top);
    _projectionMatrix[2, 2] = 1f / (far - near);
    _projectionMatrix[0, 3] = -(right + left) / (right - left);
    _projectionMatrix[1, 3] = -(bottom + top) / (bottom - top);
    _projectionMatrix[2, 3] = -near / (far - near);
  }

  public void SetPerspectiveProjection(float left, float right, float top, float bottom, float near, float far) {
    float zRange = far / (far - near);

    var result = new Matrix4();
    result.M11 = 2.0f * near / (right - left);
    result.M22 = 2.0f * near / (top - bottom);
    result.M31 = (left + right) / (left - right);
    result.M32 = (top + bottom) / (bottom - top);
    result.M33 = zRange;
    result.M34 = 1.0f;
    result.M43 = -near * zRange;

    result.M31 *= -1.0f;
    result.M32 *= -1.0f;
    result.M33 *= -1.0f;
    result.M34 *= -1.0f;

    _projectionMatrix = result;
  }

  public void SetPerspectiveProjection(float fovy, float aspect, float near, float far) {
    //_projectionMatrix = Matrix4.Identity;
    // _projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, near, far);
    _projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fovy), aspect, near, far);


    /*
    float tanHalfFovy = MathF.Tan(fovy / 2f);
    _projectionMatrix = Matrix4.Zero;
    _projectionMatrix[0, 0] = 1f / (aspect * tanHalfFovy);
    _projectionMatrix[1, 1] = 1f / (tanHalfFovy);
    _projectionMatrix[2, 2] = far / (far - near);
    _projectionMatrix[2, 3] = 1f;
    _projectionMatrix[3, 2] = -(far * near) / (far - near);
    _projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(fovy, aspect, 0.01f, 100f);
    */
  }

  public void SetViewDirection(Vector3 position, Vector3 direction, Vector3 up) {
    if (up == Vector3.Zero) {
      up = _localUp;
    }

    var w = Vector3.Normalize(direction);
    var u = Vector3.Normalize(Vector3.Cross(w, up));
    var v = Vector3.Cross(w, u);

    _viewMatrix = Matrix4.Identity;
    _viewMatrix[0, 0] = u.X;
    _viewMatrix[1, 0] = u.Y;
    _viewMatrix[2, 0] = u.Z;
    _viewMatrix[0, 1] = v.X;
    _viewMatrix[1, 1] = v.Y;
    _viewMatrix[2, 1] = v.Z;
    _viewMatrix[0, 2] = w.X;
    _viewMatrix[1, 2] = w.Y;
    _viewMatrix[2, 2] = w.Z;
    _viewMatrix[3, 0] = -Vector3.Dot(u, position);
    _viewMatrix[3, 1] = -Vector3.Dot(v, position);
    _viewMatrix[3, 2] = -Vector3.Dot(w, position);
  }

  public void SetViewTarget(Vector3 position, Vector3 target, Vector3 up) {
    if (up == Vector3.Zero) {
      up = _localUp;
    }

    SetViewDirection(position, target - position, up);
  }

  public void SetViewYXZ(Vector3 position, Vector3 rotation) {
    float c3 = MathF.Cos(rotation.Z);
    float s3 = MathF.Sin(rotation.Z);
    float c2 = MathF.Cos(rotation.X);
    float s2 = MathF.Sin(rotation.X);
    float c1 = MathF.Cos(rotation.Y);
    float s1 = MathF.Sin(rotation.Y);

    var u = new Vector3((c1 * c3 + s1 * s2 * s3), (c2 * s3), (c1 * s2 * s3 - c3 * s1));
    var v = new Vector3((c3 * s1 * s2 - c1 * s3), (c2 * c3), (c1 * c3 * s2 + s1 * s3));
    var w = new Vector3((c2 * s1), (-s2), (c1 * c2));

    _viewMatrix = Matrix4.Identity;
    _viewMatrix[0, 0] = u.X;
    _viewMatrix[1, 0] = u.Y;
    _viewMatrix[2, 0] = u.Z;
    _viewMatrix[0, 1] = v.X;
    _viewMatrix[1, 1] = v.Y;
    _viewMatrix[2, 1] = v.Z;
    _viewMatrix[0, 2] = w.X;
    _viewMatrix[1, 2] = w.Y;
    _viewMatrix[2, 2] = w.Z;
    _viewMatrix[3, 0] = -Vector3.Dot(u, position);
    _viewMatrix[3, 1] = -Vector3.Dot(v, position);
    _viewMatrix[3, 2] = -Vector3.Dot(w, position);
  }

  public Matrix4 ProjectionMatrix() {
    return _projectionMatrix;
  }

  public Matrix4 ViewMatrix() {
    //Vector3 position = Owner!.GetComponent<Transform>().Position;
    // var position = new Vector3(0, 0, -2);
    // return Matrix4.LookAt(position, position + _front, _up);
    // return Matrix4.LookAt(position, position + _front, _up);
    _viewMatrix = Matrix4.LookAt(new Vector3(0, 0, -2), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
    return _viewMatrix;
  }

  public Matrix4 GetMVP(Matrix4 modelMatrix) {
    return modelMatrix * _viewMatrix * _projectionMatrix;
  }
}