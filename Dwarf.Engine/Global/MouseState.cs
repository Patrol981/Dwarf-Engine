// using Dwarf.Extensions.GLFW;
using Dwarf.GLFW.Core;

namespace Dwarf.Engine.Globals;

public sealed class MouseState {
  private static MouseState s_instance = null!;

  public event EventHandler ClickEvent;

  private OpenTK.Mathematics.Vector2d _lastMousePositionFromCallback = new(0, 0);
  private double _previousScrollY = 0.0;
  private double _scrollDelta = 0.0;

  public unsafe static void MouseCallback(GLFWwindow* window, double xpos, double ypos) {
    GetInstance()._lastMousePositionFromCallback = new(xpos, ypos);
  }

  public unsafe static void ScrollCallback(GLFWwindow* window, double xoffset, double yoffset) {
    double currentScrollY = yoffset;
    GetInstance()._scrollDelta = currentScrollY += yoffset;
    GetInstance()._previousScrollY = currentScrollY;
  }

  public unsafe static void MouseButtonCallback(GLFWwindow* window, int button, int action, int mods) {
    if (action == 1) {
      GetInstance().OnClicked(null!);
    }
  }

  private void OnClicked(EventArgs e) {
    ClickEvent?.Invoke(this, e);
  }

  public OpenTK.Mathematics.Vector2d MousePosition => _lastMousePositionFromCallback;
  public double ScrollDelta {
    get { return _scrollDelta; }
    set { _scrollDelta = value; }
  }
  public double PreviousScroll => _previousScrollY;

  public static MouseState GetInstance() {
    s_instance ??= new MouseState();
    return s_instance;
  }
}