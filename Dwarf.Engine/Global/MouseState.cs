// using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using Dwarf.GLFW.Core;

using static Dwarf.Extensions.GLFW.MouseButtonMap;

namespace Dwarf.Globals;

public class MouseButtons {
  public bool Left { get; set; }
  public bool Right { get; set; }
  public bool Middle { get; set; }
}

public sealed class MouseState {
  private static MouseState s_instance = null!;

  public event EventHandler? ClickEvent;

  private OpenTK.Mathematics.Vector2d _lastMousePositionFromCallback = new(0, 0);

  public static unsafe void MouseCallback(GLFWwindow* window, double xpos, double ypos) {
    GetInstance()._lastMousePositionFromCallback = new(xpos, ypos);
  }

  public static unsafe void ScrollCallback(GLFWwindow* window, double xoffset, double yoffset) {
    double currentScrollY = yoffset;
    GetInstance().ScrollDelta = currentScrollY += yoffset;
    GetInstance().PreviousScroll = currentScrollY;
  }

  public static unsafe void MouseButtonCallback(GLFWwindow* window, int button, int action, int mods) {
    switch (action) {
      case (int)MouseButtonMap.Action.GLFW_PRESS:
        // GetInstance().OnClicked(null!);

        switch (button) {
          case (int)Buttons.GLFW_MOUSE_BUTTON_LEFT:
            s_instance.MouseButtons.Left = true;
            s_instance.QuickStateMouseButtons.Left = true;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_RIGHT:
            s_instance.MouseButtons.Right = true;
            s_instance.QuickStateMouseButtons.Right = true;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_MIDDLE:
            s_instance.MouseButtons.Middle = true;
            s_instance.QuickStateMouseButtons.Middle = true;
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
            s_instance.QuickStateMouseButtons.Left = false;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_RIGHT:
            // s_instance._mouseButtons.Right = false;
            s_instance.QuickStateMouseButtons.Right = false;
            break;
          case (int)Buttons.GLFW_MOUSE_BUTTON_MIDDLE:
            // s_instance._mouseButtons.Middle = false;
            s_instance.QuickStateMouseButtons.Middle = false;
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
  public double ScrollDelta { get; set; } = 0.0;
  public double PreviousScroll { get; private set; } = 0.0;

  public MouseButtons MouseButtons { get; set; } = new() {
    Left = false,
    Right = false,
    Middle = false,
  };

  public MouseButtons QuickStateMouseButtons { get; } = new() {
    Left = false,
    Right = false,
    Middle = false,
  };

  public static MouseState GetInstance() {
    s_instance ??= new MouseState();
    return s_instance;
  }
}