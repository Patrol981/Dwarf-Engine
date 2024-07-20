// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;
using Dwarf.GLFW;
using Dwarf.Windowing;

using static Dwarf.GLFW.GLFW;

namespace Dwarf.Globals;
public static class WindowState {
  public static Window s_Window = null!;
  public static InputValue s_MouseCursorState = InputValue.GLFW_CURSOR_NORMAL;

  public static unsafe void SetCursorMode(InputValue inputValue) {
    s_MouseCursorState = inputValue;
    glfwSetInputMode(s_Window.GLFWwindow, (int)InputMode.GLFW_CURSOR, (int)s_MouseCursorState);
  }

  public static void FocusOnWindow() {
    if (s_MouseCursorState == InputValue.GLFW_CURSOR_DISABLED) {
      SetCursorMode(InputValue.GLFW_CURSOR_NORMAL);
    } else {
      SetCursorMode(InputValue.GLFW_CURSOR_DISABLED);
    }
  }

  public static unsafe void MaximizeWindow() {
    Application.Instance.Device.WaitDevice();
    glfwMaximizeWindow(s_Window.GLFWwindow);
  }

  public static unsafe void CenterWindow() {
    var monitor = glfwGetPrimaryMonitor();
    var mode = glfwGetVideoMode(monitor);

    glfwGetWindowSize(s_Window.GLFWwindow, out int wW, out int wH);
    glfwSetWindowPos(s_Window.GLFWwindow, (mode->Width / 2) - (wW / 2), (mode->Height / 2) - (wH / 2));
  }
}