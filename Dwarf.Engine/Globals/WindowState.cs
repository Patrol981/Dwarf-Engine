// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;
// using Dwarf.GLFW;
using Dwarf.Windowing;

// using static Dwarf.GLFW.GLFW;
using static SDL3.SDL3;

namespace Dwarf.Globals;

public enum CursorState {
  Normal,
  Centered,
  Hidden
}

public static class WindowState {
  public static Window s_Window = null!;
  public static CursorState s_MouseCursorState = CursorState.Normal;

  public static unsafe void SetCursorMode(CursorState cursorState) {
    s_MouseCursorState = cursorState;
    switch (cursorState) {
      case CursorState.Normal:
        SDL_ShowCursor();
        SDL_SetWindowRelativeMouseMode(s_Window.SDLWindow, false);
        break;
      case CursorState.Centered:
        SDL_SetWindowRelativeMouseMode(s_Window.SDLWindow, true);
        // SDL_WarpMouseInWindow(s_Window.SDLWindow, s_Window.Size.X / 2, s_Window.Size.Y / 2);
        break;
      case CursorState.Hidden:
        SDL_SetWindowRelativeMouseMode(s_Window.SDLWindow, false);
        SDL_HideCursor();
        break;
    }
  }

  public static void FocusOnWindow() {
    if (s_MouseCursorState == CursorState.Centered) {
      SetCursorMode(CursorState.Normal);
    } else {
      SetCursorMode(CursorState.Centered);
    }
  }

  public static unsafe void MaximizeWindow() {
    Application.Instance.Device.WaitDevice();
    SDL_MaximizeWindow(s_Window.SDLWindow);
  }
}