// using Dwarf.Extensions.GLFW;
// using Dwarf.Extensions.GLFW;
using System.Numerics;
using Dwarf.Windowing;
using SDL3;
using static SDL3.SDL3;
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

  private Vector2 _lastMousePositionFromCallback = Vector2.Zero;
  private Vector2 _lastRelativeMousePositionFromCallback = Vector2.Zero;

  public static void WindowMouseCallback(float xpos, float ypos) {
    GetInstance()._lastMousePositionFromCallback.X = xpos;
    GetInstance()._lastMousePositionFromCallback.Y = ypos;
  }

  public static void RelativeMouseCallback(float xpos, float ypos) {
    GetInstance()._lastRelativeMousePositionFromCallback.X += xpos;
    GetInstance()._lastRelativeMousePositionFromCallback.Y += ypos;
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

  public Vector2 MousePosition {
    get {
      return Window.MouseCursorState switch {
        CursorState.Centered => _lastRelativeMousePositionFromCallback,
        _ => _lastMousePositionFromCallback,
      };
    }
    set {
      switch (Window.MouseCursorState) {
        case CursorState.Centered:
          _lastRelativeMousePositionFromCallback = value;
          break;
        default:
          _lastMousePositionFromCallback = value;
          break;
      }
    }
  }
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