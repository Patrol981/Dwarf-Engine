using Dwarf.Extensions.GLFW;
using OpenTK.Mathematics;

namespace Dwarf.Engine.Globals;

public sealed class KeyboardState {
  private static KeyboardState s_instance;

  public static unsafe void KeyCallback(GLFWwindow* window, int key, int scancode, int action, int mods) {
    // Console.WriteLine(key);
  }

  public KeyboardState GetInstance() {
    if (s_instance == null) {
      s_instance = new KeyboardState();
    }
    return s_instance;
  }
}