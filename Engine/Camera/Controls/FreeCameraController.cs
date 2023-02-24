using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.GLFW;
using static Dwarf.Extensions.GLFW.GLFW;

namespace Dwarf.Engine;

public class FreeCameraController : Component {
  public unsafe void Update() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);
    } else {
      var deltaX = (float)MouseState.GetInstance().MousePosition.X - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)MouseState.GetInstance().MousePosition.Y - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);

      if (WindowState.s_MouseCursorState == InputValue.GLFW_CURSOR_DISABLED) {
        Owner!.GetComponent<Camera>().Yaw += deltaX * CameraState.GetSensitivity();
        Owner!.GetComponent<Camera>().Pitch += deltaY * CameraState.GetSensitivity();
      }

      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_D) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_A) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_S) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_W) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
        Console.WriteLine(Owner.GetComponent<Transform>().Position);
      }

      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_LEFT_SHIFT) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_SPACE) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      // DEBUG remove later

      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_RIGHT) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.s_App.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(-1, 0, 0) * Time.DeltaTime);
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_LEFT) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.s_App.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(1, 0, 0) * Time.DeltaTime);
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_DOWN) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.s_App.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(0, 0, -1) * Time.DeltaTime);
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_UP) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.s_App.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(0, 0, 1) * Time.DeltaTime);
      }

      //if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_F) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
      //WindowState.FocusOnWindow();
      //}
    }
  }
}