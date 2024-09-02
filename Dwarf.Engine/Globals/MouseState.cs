// using Dwarf.Extensions.GLFW;
// using Dwarf.Extensions.GLFW;
using SDL3;
// using Dwarf.GLFW.Core;

// using static Dwarf.Extensions.GLFW.MouseButtonMap;

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

  public static unsafe void MouseCallback(double xpos, double ypos) {
    // GetInstance()._lastMousePositionFromCallback = new(xpos, ypos);
    GetInstance()._lastMousePositionFromCallback.X += xpos;
    GetInstance()._lastMousePositionFromCallback.Y += ypos;
  }

  public static unsafe void ScrollCallback(double xoffset, double yoffset) {
    double currentScrollY = yoffset;
    GetInstance().ScrollDelta = currentScrollY += yoffset;
    GetInstance().PreviousScroll = currentScrollY;
  }

  public static void MouseButtonCallbackUp(SDL_Button button) {
    switch (button) {
      case SDL_Button.Left:
        s_instance.QuickStateMouseButtons.Left = false;
        break;
      case SDL_Button.Middle:
        s_instance.QuickStateMouseButtons.Middle = false;
        break;
      case SDL_Button.Right:
        s_instance.QuickStateMouseButtons.Right = false;
        break;
      default:
        break;
    }
  }

  public static void MouseButtonCallbackDown(SDL_Button button) {
    switch (button) {
      case SDL_Button.Left:
        s_instance.MouseButtons.Left = true;
        s_instance.QuickStateMouseButtons.Left = true;
        break;
      case SDL_Button.Middle:
        s_instance.MouseButtons.Middle = true;
        s_instance.QuickStateMouseButtons.Middle = true;
        break;
      case SDL_Button.Right:
        s_instance.MouseButtons.Right = true;
        s_instance.QuickStateMouseButtons.Right = true;
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