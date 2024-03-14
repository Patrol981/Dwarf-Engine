using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;
using Dwarf.GLFW;

using OpenTK.Mathematics;

namespace Dwarf.Engine;
public class ThirdPersonCamera : DwarfScript {
  private Camera _camera;
  private float _distanceFromTarget = 2.5f;
  private float _angleAroundTarget = 0f;

  public ThirdPersonCamera() {
    FollowTarget = null!;
    _camera = null!;
  }

  public void Init(Entity followTarget) {
    FollowTarget = followTarget;
  }

  public override void Awake() {
    _camera = Owner!.GetComponent<Camera>();
  }

  public override void Update() {
    HandleMovement();
  }
  private void CalculateZoom() {
    float zoomWheel = (float)MouseState.GetInstance().ScrollDelta * 0.1f;
    _distanceFromTarget -= zoomWheel;
  }

  private void CalculatePitch(float deltaY) {
    float pichChange = deltaY * 0.1f;
    _camera.Pitch += pichChange;
  }

  private void CalculateAngle(float deltaX) {
    float angleChange = deltaX * 0.3f;
    _angleAroundTarget -= angleChange;
  }

  private float CalculateHorizontalDistance() {
    return _distanceFromTarget * MathF.Cos(MathHelper.DegreesToRadians(_camera.Pitch));
  }

  private float CalculateVerticalDistance() {
    return _distanceFromTarget * MathF.Sin(MathHelper.DegreesToRadians(_camera.Pitch));
  }

  private void CalculateCameraPosition(float horizontal, float vertical) {
    if (FollowTarget == null) return;

    float theta = _angleAroundTarget;
    float offectX = (float)(horizontal * MathF.Sin(MathHelper.DegreesToRadians(theta)));
    float offsetZ = (float)(horizontal * MathF.Cos(MathHelper.DegreesToRadians(theta)));
    Owner!.GetComponent<Transform>().Position.X = FollowTarget.GetComponent<Transform>().Position.X - offectX;
    Owner!.GetComponent<Transform>().Position.Z = FollowTarget.GetComponent<Transform>().Position.Z - offsetZ;
    Owner!.GetComponent<Transform>().Position.Y = FollowTarget.GetComponent<Transform>().Position.Y - vertical - 1.3f;
  }

  private unsafe void HandleMovement() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);

    } else {
      var deltaX = (float)MouseState.GetInstance().MousePosition.X - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)MouseState.GetInstance().MousePosition.Y - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);

      if (WindowState.s_MouseCursorState != InputValue.GLFW_CURSOR_DISABLED) return;

      CalculateZoom();
      CalculateAngle(deltaX);
      CalculatePitch(deltaY);
      float horizontal = CalculateHorizontalDistance();
      float vertical = CalculateVerticalDistance();
      CalculateCameraPosition(horizontal, vertical);
      _camera.Yaw = 90 - _angleAroundTarget;
    }
  }

  public Entity FollowTarget { get; set; }

}
