using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.EntityComponentSystem.Lightning;
using Dwarf.Globals;
using Dwarf.Windowing;
// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;

namespace Dwarf;

public class FreeCameraController : DwarfScript {
  public override void Update() {
    var useController = 0;
    if (useController == 1) {

    } else {
      MoveByPC();
      LightHandler();
    }
  }
  public unsafe void MoveByPC() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(Input.MousePosition);
    } else {
      var deltaX = (float)Input.MousePosition.X - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)Input.MousePosition.Y - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(Input.MousePosition);

      if (Window.MouseCursorState == CursorState.Centered) {
        Owner!.GetComponent<Camera>().Yaw += deltaX * CameraState.GetSensitivity();
        Owner!.GetComponent<Camera>().Pitch += deltaY * CameraState.GetSensitivity();
      }

      if (Input.GetKey(Scancode.D)) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.A)) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.S)) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.W)) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.Space)) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.LeftShift)) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      //if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_F) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
      //WindowState.FocusOnWindow();
      //}
    }
  }

  public void LightHandler() {
    if (Input.GetKeyDown(Keycode.L)) {
      var app = Application.Instance;
      var light = new Entity() {
        Name = "pointlight"
      };
      light.AddTransform(Owner.GetComponent<Transform>().Position);
      light.AddComponent(new PointLightComponent());
      light.GetComponent<PointLightComponent>().Color = new Vector4(
        1,
        1f,
        1f,
        0.4f
      );
      app.AddEntity(light);
    }
  }
}