using Dwarf.EntityComponentSystem;
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
    }
  }
  public unsafe void MoveByPC() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);
    } else {
      var deltaX = (float)MouseState.GetInstance().MousePosition.X - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)MouseState.GetInstance().MousePosition.Y - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);

      if (Window.MouseCursorState == CursorState.Centered) {
        Owner!.GetComponent<Camera>().Yaw += deltaX * CameraState.GetSensitivity();
        Owner!.GetComponent<Camera>().Pitch += deltaY * CameraState.GetSensitivity();
      }

      if (Input.GetKey(SDL3.SDL_Scancode.D)) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(SDL3.SDL_Scancode.A)) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(SDL3.SDL_Scancode.S)) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(SDL3.SDL_Scancode.W)) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(SDL3.SDL_Scancode.Space)) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(SDL3.SDL_Scancode.LeftShift)) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      //if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_F) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
      //WindowState.FocusOnWindow();
      //}
    }
  }
}