using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Windowing;

// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;
using OpenTK.Mathematics;

namespace Dwarf;
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
    float zoomWheel = (float)Input.ScrollDelta * 0.1f;
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

    var transform = Owner!.GetComponent<Transform>();
    var targetPos = FollowTarget.GetComponent<Transform>().Position;

    // Owner!.GetComponent<Transform>().Position.X = FollowTarget.GetComponent<Transform>().Position.X - offectX;
    // Owner!.GetComponent<Transform>().Position.Z = FollowTarget.GetComponent<Transform>().Position.Z - offsetZ;
    // Owner!.GetComponent<Transform>().Position.Y = FollowTarget.GetComponent<Transform>().Position.Y - vertical - 1.3f;

    transform.Position = new(targetPos.X - offectX, targetPos.Y - vertical - 1.3f, targetPos.Z - offsetZ);
  }

  private unsafe void HandleMovement() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(Input.MousePosition);

    } else {
      var deltaX = (float)Input.MousePosition.X - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)Input.MousePosition.Y - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(Input.MousePosition);

      if (Window.MouseCursorState != CursorState.Centered) return;

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
