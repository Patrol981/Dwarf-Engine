using Dwarf.GLFW;
using static Dwarf.GLFW.GLFW;

using Dwarf.Extensions.GLFW;

namespace Dwarf.Engine.Globals;
public static class Input {
  public unsafe static bool GetKey(Keys key) {
    if (glfwGetKey(WindowState.s_Window.GLFWwindow, (int)key) == (int)KeyAction.GLFW_PRESS) {
      return true;
    }
    return false;
  }

  public unsafe static bool GetKeyDown(Keys key) {
    var keyboardState = KeyboardState.Instance;

    bool keyPressed = keyboardState.KeyStates[(int)key].KeyPressed;
    keyboardState.KeyStates[(int)key].KeyPressed = false;

    return keyPressed;
  }

  public unsafe static bool GetMouseButtonDown(MouseButtonMap.Buttons button) {
    if (glfwGetMouseButton(WindowState.s_Window.GLFWwindow, (int)button) == (int)MouseButtonMap.Action.GLFW_PRESS) {
      return true;
    }
    return false;
  }
}
