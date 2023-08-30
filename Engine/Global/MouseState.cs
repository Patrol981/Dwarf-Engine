using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;

using OpenTK.Mathematics;

namespace Dwarf.Engine.Globals;

public sealed class MouseState {
  private static MouseState s_instance = null!;

  public event EventHandler ClickEvent;

  private Vector2d _lastMousePositionFromCallback = new(0, 0);
  // private Vector2d _scrollDelta = new(0, 0);
  private double _previousScrollY = 0.0;
  private double _scrollDelta = 0.0;

  public unsafe static void MouseCallback(GLFWwindow* window, double xpos, double ypos) {
    MouseState.GetInstance()._lastMousePositionFromCallback = new(xpos, ypos);
  }

  public unsafe static void ScrollCallback(GLFWwindow* window, double xoffset, double yoffset) {
    double currentScrollY = yoffset;
    // MouseState.GetInstance()._scrollDelta = currentScrollY - MouseState.GetInstance()._previousScrollY;
    MouseState.GetInstance()._scrollDelta = currentScrollY += yoffset;
    MouseState.GetInstance()._previousScrollY = currentScrollY;
    // Logger.Info($"[SCROLL CHANGE] {MouseState.GetInstance()._scrollDelta}");
  }

  public unsafe static void MouseButtonCallback(GLFWwindow* window, int button, int action, int mods) {
    // Logger.Info($"[Button Action] {button} {action} {mods}");
    if (action == 1) {
      MouseState.GetInstance().OnClicked(null!);
    }
  }

  private void OnClicked(EventArgs e) {
    ClickEvent?.Invoke(this, e);
    Logger.Info("CLICK");
  }

  public Vector2d MousePosition => _lastMousePositionFromCallback;
  public double ScrollDelta {
    get { return _scrollDelta; }
    set { _scrollDelta = value; }
  }
  public double PreviousScroll => _previousScrollY;

  public static MouseState GetInstance() {
    if (s_instance == null) {
      s_instance = new MouseState();
    }
    return s_instance;
  }
}