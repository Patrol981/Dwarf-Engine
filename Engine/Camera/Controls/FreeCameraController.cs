using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.GLFW;
using static Dwarf.Extensions.GLFW.GLFW;
using Dwarf.Extensions.Logging;

using static Dwarf.Extensions.GLFW.GLFW;

namespace Dwarf.Engine;

public class FreeCameraController : Component {
  public unsafe void Update() {
    var useController = glfwJoystickIsGamepad(0);
    if (useController == 1) {
      MoveByController();
    } else {
      MoveByPC();
    }
  }

  public unsafe void MoveByController() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(new(0, 0));
    } else {
      int axesCount = 0;
      var axes = glfwGetJoystickAxes(0, &axesCount);
      int buttonsCount = 0;
      var buttons = glfwGetJoystickButtons(0, &buttonsCount);

      // axes 0 = movement left right
      // axes 1 = movement up down
      // axes 2 = camera left right
      // axes 3 = camera up down
      // axes 4,5 = triggers


      // Logger.Info($"axes {axes[2]}");

      var deltaX = (float)axes[2] - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)axes[3] - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(new(axes[2], axes[3]));

      if (axes[2] > 0.1) {
        Owner!.GetComponent<Camera>().Yaw += axes[2] * CameraState.GetSensitivity();
      }
      if (axes[2] < -0.1) {
        Owner!.GetComponent<Camera>().Yaw += axes[2] * CameraState.GetSensitivity();
      }
      if (axes[3] > 0.1) {
        Owner!.GetComponent<Camera>().Pitch += axes[3] * CameraState.GetSensitivity();
      }
      if (axes[3] < -0.1) {
        Owner!.GetComponent<Camera>().Pitch += axes[3] * CameraState.GetSensitivity();
      }


      if (axes[0] > 0.1) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (axes[0] < -0.1) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (axes[1] > 0.1) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (axes[1] < -0.1) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      // Logger.Info($"{buttonsCount}");

      // buttons 0 = a,b
      // buttons 1 = x,y
      // buttons 2 = bumpers
      // buttons 3 = 
      // buttons 4 = 
      // buttons 5 = 
      // buttons 6 = 
      // buttons 7 = 
      // buttons 8 = 
      // buttons 9 = 
      // buttons 10 =
      // buttons 11 = 
      // buttons 12 = 
      // buttons 13 = 

      // Logger.Info($"{(int)buttons[2]}");

      // buttons
      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[0]) {
        Logger.Info("A");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[1]) {
        Logger.Info("X");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_GAMEPAD_MIRROR == (int)buttons[0]) {
        Logger.Info("B");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_GAMEPAD_MIRROR == (int)buttons[1]) {
        Logger.Info("Y");
      }



      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[4]) {
        Logger.Info("Left Analog");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_GAMEPAD_MIRROR == (int)buttons[4]) {
        Logger.Info("Left Analog");
      }

      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[5]) {
        Logger.Info("Directional Up");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[6]) {
        Logger.Info("Directional Down");
      }


      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[2]) {
        Logger.Info("Left Bumper");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_GAMEPAD_MIRROR == (int)buttons[2]) {
        Logger.Info("Right Bumper");
      }

      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[3]) {
        Logger.Info("Menu");
      }

      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[7]) {
        Logger.Info("????");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[8]) {
        Logger.Info("??");
      }
      if ((int)GLFWKeyMap.KeyAction.GLFW_PRESS == (int)buttons[9]) {
        Logger.Info("???");
      }

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

      if (WindowState.s_MouseCursorState == InputValue.GLFW_CURSOR_DISABLED) {
        Owner!.GetComponent<Camera>().Yaw += deltaX * CameraState.GetSensitivity();
        Owner!.GetComponent<Camera>().Pitch += deltaY * CameraState.GetSensitivity();
      }

      // Console.WriteLine(Owner!.GetComponent<Camera>().Yaw);

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
      }

      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_LEFT_SHIFT) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_SPACE) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      // DEBUG remove later

      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_RIGHT) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.Instance.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(-1, 0, 0) * Time.DeltaTime);
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_LEFT) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.Instance.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(1, 0, 0) * Time.DeltaTime);
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_DOWN) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.Instance.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(0, 0, -1) * Time.DeltaTime);
      }
      if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_UP) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        ApplicationState.Instance.GetEntities()[0].GetComponent<Transform>().IncreasePosition(new OpenTK.Mathematics.Vector3(0, 0, 1) * Time.DeltaTime);
      }

      //if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_F) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
      //WindowState.FocusOnWindow();
      //}
    }
  }
}