using System.Diagnostics;
using System.Runtime.InteropServices;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Utils;
using SDL3;
using StbImageSharp;
using Vortice.Vulkan;

using static SDL3.SDL3;

namespace Dwarf.Windowing;

[Flags]
public enum WindowFlags {
  None = 0,
  Fullscreen = 1 << 0,
  Borderless = 1 << 1,
  Resizable = 1 << 2,
  Minimized = 1 << 3,
  Maximized = 1 << 4,
}

public enum CursorState {
  Normal,
  Centered,
  Hidden
}

public class Window : IDisposable {
  public VkUtf8String AppName = "Dwarf App"u8;
  public VkUtf8String EngineName = "Dwarf Engine"u8;
  private DwarfExtent2D _extent;
  private readonly bool _windowMinimalized = false;

  private SDL_Cursor _cursor;

  internal void Init(string windowName, bool fullscreen, int width, int height, bool debug = false) {
    InitWindow(windowName, fullscreen, debug, width, height);
    LoadIcons();
    Show();
    RefreshRate = GetRefreshRate();
    Logger.Info($"[WINDOW] Refresh rate set to {RefreshRate}");
    // EnumerateAvailableGameControllers();
  }

  private unsafe void InitWindow(string windowName, bool fullscreen, bool debug, int width, int height) {
    if (!SDL_Init(SDL_InitFlags.Video | SDL_InitFlags.Gamepad | SDL_InitFlags.Audio)) {
      throw new Exception("Failed to initalize Window");
    }

    if (debug) {
      Logger.Info("Setting Debug For SDL");
      SDL_SetLogPriorities(SDL_LogPriority.Verbose);
      SDL_SetLogOutputFunction(LogSDL);
    }

    if (!SDL_Vulkan_LoadLibrary()) {
      throw new Exception("Failed to initialize Vulkan");
    }

    var windowFlags = SDL_WindowFlags.Vulkan |
                      // SDL_WindowFlags.Maximized |
                      // SDL_WindowFlags.Transparent |
                      SDL_WindowFlags.Occluded |
                      SDL_WindowFlags.MouseFocus |
                      SDL_WindowFlags.InputFocus |
                      SDL_WindowFlags.Resizable;

    if (fullscreen) {
      windowFlags |= SDL_WindowFlags.Fullscreen | SDL_WindowFlags.Borderless;
    }

    SDLWindow = SDL_CreateWindow(windowName, width, height, windowFlags);
    if (SDLWindow.IsNull) {
      throw new Exception("Failed to create SDL window");
    }

    _ = SDL_SetWindowPosition(SDLWindow, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);
    // SDL_SetWindowOpacity(SDLWindow, 0.1f);

    Application.Instance.Window.Extent = new DwarfExtent2D((uint)width, (uint)height);
  }

  private unsafe void LoadIcons() {
    var engineIcoStream = File.OpenRead($"{DwarfPath.AssemblyDirectory}/Resources/ico/dwarf_ico.png");
    var engineIco = ImageResult.FromStream(engineIcoStream, ColorComponents.RedGreenBlueAlpha);
    var engineSurface = SDL_CreateSurface(engineIco.Width, engineIco.Height, SDL_PixelFormat.Abgr8888);
    fixed (byte* pixPtr = engineIco.Data) {
      engineSurface->pixels = (nint)pixPtr;
    }
    if (!SDL_SetWindowIcon(SDLWindow, engineSurface)) {
      throw new Exception("Failed to load window icon");
    }
    Marshal.FreeHGlobal((nint)engineSurface);
    engineIcoStream.Dispose();

    var cursorIcoStream = File.OpenRead($"{DwarfPath.AssemblyDirectory}/Resources/ico/cursor.png");
    var cursorIco = ImageResult.FromStream(cursorIcoStream, ColorComponents.RedGreenBlueAlpha);
    var cursorSurface = SDL_CreateSurface(cursorIco.Width, cursorIco.Height, SDL_PixelFormat.Argb8888);
    fixed (byte* cursorPtr = cursorIco.Data) {
      cursorSurface->pixels = (nint)cursorPtr;
    }
    _cursor = SDL_CreateColorCursor(cursorSurface, 0, 0);
    SDL_SetCursor(_cursor);
    cursorIcoStream.Dispose();
  }

  public void Terminate() {

  }

  public void Show() {
    _ = SDL_ShowWindow(SDLWindow);
  }

  public void Dispose() {
    SDL_DestroyWindow(SDLWindow);
  }

  public void ResetWindowResizedFlag() {
    FramebufferResized = false;
  }

  public void PollEvents() {
    while (SDL_PollEvent(out SDL_Event e)) {
      switch (e.type) {
        case SDL_EventType.Quit:
          ShouldClose = true;
          break;
        case SDL_EventType.KeyDown:
          Input.KeyCallback(SDLWindow, e.key, e.type);
          break;
        case SDL_EventType.GamepadButtonDown:
          Input.GamepadCallback(SDLWindow, e.gbutton, e.type);
          break;
        case SDL_EventType.MouseMotion:
          switch (MouseCursorState) {
            case CursorState.Centered:
              Input.RelativeMouseCallback(e.motion.xrel, e.motion.yrel);
              break;
            default:
              Input.WindowMouseCallback(e.motion.x, e.motion.y);
              break;
          }
          break;
        case SDL_EventType.MouseWheel:
          Input.ScrollCallback(e.wheel.x, e.wheel.y);
          break;
        case SDL_EventType.MouseButtonUp:
          Input.MouseButtonCallbackUp(e.button.Button);
          break;
        case SDL_EventType.MouseButtonDown:
          Input.MouseButtonCallbackDown(e.button.Button);
          break;
        case SDL_EventType.WindowResized:
          FrambufferResizedCallback(e.window.data1, e.window.data2);
          break;
        case SDL_EventType.WindowMaximized:
          FrambufferResizedCallback(e.window.data1, e.window.data2);
          break;
        case SDL_EventType.WindowRestored:
          IsMinimalized = false;
          // FrambufferResizedCallback(e.window.data1, e.window.data2);
          break;
        case SDL_EventType.WindowMinimized:
          IsMinimalized = true;
          break;
        case SDL_EventType.LowMemory:
          throw new Exception("Memory Leak");
        case SDL_EventType.GamepadAdded:
          if (GameController.IsNull) {
            GameController = SDL_OpenGamepad(e.gdevice.which);
            Logger.Info($"[SDL] Connected {SDL_GetGamepadName(GameController)}");
          }
          break;
        case SDL_EventType.GamepadRemoved:
          var instanceId = e.gdevice.which;
          if (GameController.IsNotNull && SDL_GetGamepadID(GameController) == instanceId) {
            Logger.Info($"[SDL] Disconnected {SDL_GetGamepadName(GameController)}");
            SDL_CloseGamepad(GameController);
          }
          break;
        default:
          break;
      }
    }
  }

  public void WaitEvents() {
    // SDL_WaitEvent()
  }

  private static unsafe void FrambufferResizedCallback(int width, int height) {
    if (width <= 0 || height <= 0) return;
    Logger.Info($"RESISING {width} {height}");
    Application.Instance.Window.FramebufferResized = true;
    Application.Instance.Window.Extent = new DwarfExtent2D((uint)width, (uint)height);
    Application.Instance.Window.OnResizedEvent(null!);
  }

  private static void LogSDL(SDL_LogCategory category, SDL_LogPriority priority, string? description) {
    if (priority >= SDL_LogPriority.Error) {
      Logger.Error($"[{priority}] SDL: {description}");
      throw new Exception(description);
    } else {
      Logger.Info($"[{priority}] SDL: {description}");
    }
  }

  private void OnResizedEvent(EventArgs e) {
    Application.Instance.Window.OnResizedEventDispatcher?.Invoke(this, e);
  }

  public bool WasWindowResized() => FramebufferResized;
  public bool WasWindowMinimalized() => _windowMinimalized;

  public unsafe VkSurfaceKHR CreateSurface(VkInstance instance) {
    VkSurfaceKHR surface;
    return SDL_Vulkan_CreateSurface(SDLWindow, instance, IntPtr.Zero, (ulong**)&surface) == false
      ? throw new Exception("Failed to create SDL Surface")
      : surface;
  }

  public unsafe float GetRefreshRate() {
    var displays = SDL_GetDisplays();
    var displayMode = SDL_GetCurrentDisplayMode(displays[0]);
    return displayMode->refresh_rate;
  }

  public static unsafe void SetCursorMode(CursorState cursorState) {
    var prevMousePos = Input.MousePosition;

    MouseCursorState = cursorState;

    Logger.Info($"Setting cursor state to: {MouseCursorState}");

    switch (cursorState) {
      case CursorState.Normal:
        SDL_SetWindowRelativeMouseMode(Application.Instance.Window.SDLWindow, false);
        break;
      case CursorState.Centered:
        SDL_SetWindowRelativeMouseMode(Application.Instance.Window.SDLWindow, true);
        Input.MousePosition = prevMousePos;
        // SDL_WarpMouseInWindow(s_Window.SDLWindow, s_Window.Size.X / 2, s_Window.Size.Y / 2);
        break;
      case CursorState.Hidden:
        SDL_SetWindowRelativeMouseMode(Application.Instance.Window.SDLWindow, false);
        SDL_HideCursor();
        break;
    }
  }

  public static void FocusOnWindow() {
    if (MouseCursorState == CursorState.Centered) {
      SetCursorMode(CursorState.Normal);
    } else {
      SetCursorMode(CursorState.Centered);
    }
  }

  public static unsafe void MaximizeWindow() {
    Application.Instance.Device.WaitDevice();
    SDL_MaximizeWindow(Application.Instance.Window.SDLWindow);
  }

  public void EnumerateAvailableGameControllers() {
    var gamepads = SDL_GetJoysticks();
    foreach (var gamepad in gamepads) {
      if (SDL_IsGamepad(gamepad)) {
        GameController = SDL_OpenGamepad(gamepad);
        Logger.Info($"[SDL] Connected {SDL_GetGamepadName(GameController)}");
      }
    }
  }

  public DwarfExtent2D Extent {
    get { return _extent; }
    private set { _extent = value; }
  }
  public bool ShouldClose { get; set; } = false;
  public bool FramebufferResized { get; private set; } = false;
  public bool IsMinimalized { get; private set; } = false;
  public event EventHandler? OnResizedEventDispatcher;
  public float RefreshRate { get; private set; }
  public static SDL_Gamepad GameController { get; private set; }
  public SDL_Window SDLWindow { get; private set; }
  public static CursorState MouseCursorState = CursorState.Normal;
}