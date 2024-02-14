// using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using Dwarf.GLFW.Core;

using static Dwarf.Extensions.GLFW.MouseButtonMap;
using Dwarf.Extensions.GLFW;

namespace Dwarf.Engine.Globals;

public class MouseButtons {
  public bool Left { get; set; }
  public bool Right { get; set; }
  public bool Middle { get; set; }
}

public sealed class MouseState {
  private static MouseState s_instance = null!;

  public event EventHandler ClickEvent;

  private OpenTK.Mathematics.Vector2d _lastMousePositionFromCallback = new(0, 0);
  private double _previousScrollY = 0.0;
  private double _scrollDelta = 0.0;
  private MouseButtons _mouseButtons = new() {
    Left = false,
    Right = false,
    Middle = false,
  };
  private MouseButtons _quickStateMouseButtons = new() {
    Left = false,
    Right = false,
    Middle = false,
  };

  public unsafe static void MouseCallback(GLFWwindow* window, double xpos, double ypos) {
    GetInstance()._lastMousePositionFromCallback = new(xpos, ypos);
  }

  public unsafe static void ScrollCallback(GLFWwindow* window, double xoffset, double yoffset) {
    double currentScrollY = yoffset;
    GetInstance()._scrollDelta = currentScrollY += yoffset;
    GetInstance()._previousScrollY = currentScrollY;
  }

  public unsafe static void MouseButtonCallback(GLFWwindow* window, int button, int action, int mods) {
    switch (action) {
      case (int)MouseButtonMap.Action.GLFW_PRESS:
        GetInstance().OnClicked(null!);

        switch (button) {
          case (int)Buttons.GLFW_MOUSE_BUTTON_LEFT:
            s_instance._mouseButtons.Left = true;
            s_instance._quickStateMouseButtons.Left = true;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_RIGHT:
            s_instance._mouseButtons.Right = true;
            s_instance._quickStateMouseButtons.Right = true;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_MIDDLE:
            s_instance._mouseButtons.Middle = true;
            s_instance._quickStateMouseButtons.Middle = true;
            break;
          default:
            Logger.Error("Unknown mouse button key");
            break;
        }
        break;
      case (int)MouseButtonMap.Action.GLFW_RELEASE:
        switch (button) {
          case (int)Buttons.GLFW_MOUSE_BUTTON_LEFT:
            // s_instance._mouseButtons.Left = false;
            s_instance._quickStateMouseButtons.Left = false;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_RIGHT:
            // s_instance._mouseButtons.Right = false;
            s_instance._quickStateMouseButtons.Right = false;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_MIDDLE:
            // s_instance._mouseButtons.Middle = false;
            s_instance._quickStateMouseButtons.Middle = false;
            break;
          default:
            Logger.Error("Unknown mouse button key");
            break;
        }
        break;
      default:
        break;
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

  public MouseButtons MouseButtons {
    get { return _mouseButtons; }
    set { _mouseButtons = value; }
  }

  public MouseButtons QuickStateMouseButtons {
    get { return _quickStateMouseButtons; }
  }

  public static MouseState GetInstance() {
    s_instance ??= new MouseState();
    return s_instance;
  }
}