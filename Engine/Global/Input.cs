using Dwarf.Extensions.GLFW;

using static Dwarf.Extensions.GLFW.GLFW;

namespace Dwarf.Engine.Globals;
public static class Input {
  public unsafe static bool GetKeyDown(GLFWKeyMap.Keys key) {
    if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)key) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
      return true;
    }
    return false;
  }

  public unsafe static bool GetMouseButtonDown(MouseButtonMap.Buttons button) {
    if (glfwGetMouseButton(WindowState.s_Window.GLFWwindow, (int)button) == (int)MouseButtonMap.Action.GLFW_PRESS) {
      return true;
    }
    return false;
  }
}
