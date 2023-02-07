using Dwarf.Extensions.GLFW;
using OpenTK.Mathematics;

namespace Dwarf.Engine.Globals;

public sealed class KeyboardState {
  private static KeyboardState s_instance;

  public static unsafe void KeyCallback(GLFWwindow* window, int key, int scancode, int action, int mods) {
    // Console.WriteLine(key);
    switch (action) {
      case (int)GLFWKeyMap.KeyAction.GLFW_PRESS:
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_F) WindowState.FocusOnWindow();
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_F1) WindowState.MaximizeWindow();
        break;
    }
  }

  public KeyboardState GetInstance() {
    if (s_instance == null) {
      s_instance = new KeyboardState();
    }
    return s_instance;
  }
}