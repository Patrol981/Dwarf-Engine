using Dwarf.Extensions.GLFW;

using static Dwarf.GLFW.GLFW;

namespace Dwarf.Globals;
public static class Input {
  public static unsafe bool GetKey(Keys key) {
    return glfwGetKey(WindowState.s_Window.GLFWwindow, (int)key) == (int)KeyAction.GLFW_PRESS;
  }

  public static unsafe bool GetKeyDown(Keys key) {
    var keyboardState = KeyboardState.Instance;

    bool keyPressed = keyboardState.KeyStates[(int)key].KeyPressed;
    keyboardState.KeyStates[(int)key].KeyPressed = false;

    return keyPressed;
  }

  public static bool GetMouseButtonDown(MouseButtonMap.Buttons button) {
    var mouseState = MouseState.GetInstance();
    bool mouseBtnPressed = false;

    switch (button) {
      case MouseButtonMap.Buttons.GLFW_MOUSE_BUTTON_LEFT:
        mouseBtnPressed = mouseState.MouseButtons.Left;
        mouseState.MouseButtons.Left = false;
        return mouseBtnPressed;
      case MouseButtonMap.Buttons.GLFW_MOUSE_BUTTON_MIDDLE:
        mouseBtnPressed = mouseState.MouseButtons.Middle;
        mouseState.MouseButtons.Middle = false;
        return mouseBtnPressed;
      case MouseButtonMap.Buttons.GLFW_MOUSE_BUTTON_RIGHT:
        mouseBtnPressed = mouseState.MouseButtons.Right;
        mouseState.MouseButtons.Right = false;
        return mouseBtnPressed;
      default:
        return mouseBtnPressed;
    }
  }

  public static unsafe bool GetMouseButton(MouseButtonMap.Buttons button) {
    return glfwGetMouseButton(WindowState.s_Window.GLFWwindow, (int)button) == (int)MouseButtonMap.Action.GLFW_PRESS;
  }
}
