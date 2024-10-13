// using Dwarf.Extensions.GLFW;
using Dwarf.Rendering.UI;

using SDL3;

using static SDL3.SDL3;
// using static Dwarf.GLFW.GLFW;

namespace Dwarf.Globals;
public static class Input {
  public static unsafe bool GetKey(SDL_Scancode scancode) {
    var state = SDL_GetKeyboardState(null);
    return state[(int)scancode];
  }

  public static unsafe bool GetKeyDown(SDL_Scancode scancode) {
    var keyboardState = KeyboardState.Instance;

    bool keyPressed = keyboardState.KeyStates[(int)scancode].KeyPressed;
    keyboardState.KeyStates[(int)scancode].KeyPressed = false;

    return keyPressed;
  }

  public static bool GetMouseButtonDown(SDL_Button button) {
    var mouseState = MouseState.GetInstance();
    bool mouseBtnPressed = false;

    switch (button) {
      case SDL_Button.Left:
        mouseBtnPressed = mouseState.MouseButtons.Left;
        mouseState.MouseButtons.Left = false;
        return mouseBtnPressed;
      case SDL_Button.Middle:
        mouseBtnPressed = mouseState.MouseButtons.Middle;
        mouseState.MouseButtons.Middle = false;
        return mouseBtnPressed;
      case SDL_Button.Right:
        mouseBtnPressed = mouseState.MouseButtons.Right;
        mouseState.MouseButtons.Right = false;
        return mouseBtnPressed;
      default:
        return mouseBtnPressed;
    }
  }

  public static unsafe bool GetMouseButton(SDL_Button button) {
    var state = SDL_GetMouseState(null, null);
    return (state & SDL_BUTTON(button)) != 0;
  }

  public static bool MouseOverUI() {
    return ImGuiController.MouseOverUI();
  }
}
