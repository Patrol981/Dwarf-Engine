using Dwarf.Extensions.GLFW;
using OpenTK.Mathematics;

namespace Dwarf.Engine.Globals;

public sealed class MouseState {
  private static MouseState s_instance = null!;

  private Vector2d _lastMousePositionFromCallback = new(0, 0);

  public unsafe static void MouseCallback(GLFWwindow* window, double xpos, double ypos) {
    MouseState.GetInstance()._lastMousePositionFromCallback = new(xpos, ypos);
  }

  public Vector2d MousePosition => _lastMousePositionFromCallback;

  public static MouseState GetInstance() {
    if (s_instance == null) {
      s_instance = new MouseState();
    }
    return s_instance;
  }
}