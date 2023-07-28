using System.Security.Cryptography;

using Assimp;

using Dwarf.Engine.EntityComponentSystem;

using OpenTK.Mathematics;

namespace Dwarf.Engine;

public enum CameraType {
  None,
  Perspective,
  Orthographic
}

public class Camera : Component {
  private Matrix4 _projectionMatrix = Matrix4.Identity;
  private Matrix4 _viewMatrix = Matrix4.Identity;

  protected Vector3 _front = -Vector3.UnitZ;
  protected Vector3 _up = -Vector3.UnitY;
  protected Vector3 _right = Vector3.UnitX;

  protected CameraType _cameraType = CameraType.None;

  internal float _pitch = 0f;
  internal float _yaw = -MathHelper.PiOver2; // Without this, you would be started rotated 90 degrees right.
  internal float _fov = MathHelper.PiOver2;
  internal float _aspect = 1;

  public Camera() { }

  public Camera(float fov, float aspect) {
    _fov = fov;
    _aspect = aspect;
  }

  /*
  public void SetOrthograpicProjection(float left, float right, float top, float bottom, float near, float far) {
    _projectionMatrix = Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, near, far);
    _cameraType = CameraType.Orthographic;
  }
  */

  public void SetOrthograpicProjection(float near, float far) {
    float scale = 3f;
    _projectionMatrix = Matrix4.CreateOrthographicOffCenter(
      -_aspect * scale,
      _aspect * scale,
      -scale,
      scale,
      near,
      far
    );
    _cameraType = CameraType.Orthographic;
  }

  public void SetOrthograpicProjection() {
    SetOrthograpicProjection(_aspect, -_aspect, -1, 1, 0.1f, 100f);
  }

  public void SetOrthograpicProjection(float left, float right, float top, float bottom, float near, float far) {
    _projectionMatrix = Matrix4.Identity;
    _projectionMatrix[0, 0] = 2.0f / (right - left);
    _projectionMatrix[1, 1] = 2.0f / (bottom - top);
    _projectionMatrix[2, 2] = 1.0f / (far - near);
    _projectionMatrix[0, 3] = -(right + left) / (right - left);
    _projectionMatrix[1, 3] = -(bottom + top) / (bottom - top);
    _projectionMatrix[2, 3] = -near / (far - near);
    // _projectionMatrix = Matrix4.CreateOrthographicOffCenter(0, 1, 0, 1, 0.1f, 1000.0f);
    _cameraType = CameraType.Orthographic;
  }

  public void SetPerspectiveProjection(float near, float far) {
    _projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(_fov), _aspect, near, far);
    _cameraType = CameraType.Perspective;
  }

  public Matrix4 GetProjectionMatrix() {
    return _projectionMatrix;
  }

  public Matrix4 GetViewMatrix() {
    Vector3 position = Owner!.GetComponent<Transform>().Position;
    _viewMatrix = Matrix4.Identity;
    _viewMatrix = Matrix4.LookAt(position, position + _front, _up);
    return _viewMatrix;
  }

  public Matrix4 GetMVP(Matrix4 modelMatrix) {
    return modelMatrix * GetViewMatrix() * GetProjectionMatrix();
  }

  public float Pitch {
    get => MathHelper.RadiansToDegrees(_pitch);
    set {
      var angle = MathHelper.Clamp(value, -89f, 89f);
      _pitch = MathHelper.DegreesToRadians(angle);
      UpdateVectors();
    }
  }

  public float Yaw {
    get => MathHelper.RadiansToDegrees(_yaw);
    set {
      _yaw = MathHelper.DegreesToRadians(value);
      UpdateVectors();
    }
  }
  public float Fov {
    get => MathHelper.RadiansToDegrees(_fov);
    set {
      var angle = MathHelper.Clamp(value, 1f, 45f);
      _fov = MathHelper.DegreesToRadians(angle);
    }
  }

  public void UpdateVectors() {
    _front.X = MathF.Cos(_pitch) * MathF.Cos(_yaw);
    _front.Y = MathF.Sin(_pitch);
    _front.Z = MathF.Cos(_pitch) * MathF.Sin(_yaw);

    _front = Vector3.Normalize(_front);

    _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY));
    _up = Vector3.Normalize(Vector3.Cross(_right, _front));
  }

  public void UpdateControls() {
    Owner!.GetComponent<FreeCameraController>()?.Update();
    Owner!.GetComponent<ThirdPersonCamera>()?.Update();
  }

  public float Aspect {
    get { return _aspect; }
    set { _aspect = value; }
  }

  public Vector3 Front => _front;
  public Vector3 Right => _right;
  public Vector3 Up => _up;
  public CameraType CameraType => _cameraType;
}