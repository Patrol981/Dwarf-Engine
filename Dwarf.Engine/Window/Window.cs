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

// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;

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

public class Window : IDisposable {
  public VkUtf8String AppName = "Dwarf App"u8;
  public VkUtf8String EngineName = "Dwarf Engine"u8;
  private DwarfExtent2D _extent;
  private readonly bool _windowMinimalized = false;

  private SDL_Cursor _cursor;

  public Window(int width, int height, string windowName, bool fullscreen, bool debug = false) {
    Size = new Vector2I(width, height);
    InitWindow(windowName, fullscreen, debug);
    LoadIcons();
    Show();
  }

  private unsafe void InitWindow(string windowName, bool fullscreen, bool debug) {
    if (!SDL_Init(SDL_InitFlags.Video)) {
      throw new Exception("Failed to initalize Window");
    }

    if (debug) {
      SDL_SetLogPriorities(SDL_LogPriority.Debug);
      SDL_SetLogOutputFunction(Log_SDL);
    }

    if (!SDL_Vulkan_LoadLibrary()) {
      throw new Exception("Failed to initialize Vulkan");
    }

    var windowFlags = SDL_WindowFlags.Vulkan |
                      SDL_WindowFlags.Resizable;

    if (fullscreen) {
      windowFlags |= SDL_WindowFlags.Fullscreen | SDL_WindowFlags.Borderless;
    }

    SDLWindow = SDL_CreateWindow(windowName, Size.X, Size.Y, windowFlags);
    if (SDLWindow.IsNull) {
      throw new Exception("Failed to create SDL window");
    }

    _ = SDL_SetWindowPosition(SDLWindow, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);

    WindowState.s_Window = this;
    WindowState.s_Window.Extent = new DwarfExtent2D((uint)Size.X, (uint)Size.Y);
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
    _ = SDL_PollEvent(out SDL_Event e);

    switch (e.type) {
      case SDL_EventType.Quit:
        ShouldClose = true;
        break;
      case SDL_EventType.KeyDown:
        KeyboardState.KeyCallback(SDLWindow, e.key, e.type);
        break;
      case SDL_EventType.MouseMotion:
        switch (WindowState.CursorState) {
          case CursorState.Centered:
            MouseState.RelativeMouseCallback(e.motion.xrel, e.motion.yrel);
            break;
          default:
            MouseState.WindowMouseCallback(e.motion.x, e.motion.y);
            break;
        }
        break;
      case SDL_EventType.MouseWheel:
        MouseState.ScrollCallback(e.wheel.x, e.wheel.y);
        break;
      case SDL_EventType.MouseButtonUp:
        MouseState.MouseButtonCallbackUp(e.button.Button);
        break;
      case SDL_EventType.MouseButtonDown:
        MouseState.MouseButtonCallbackDown(e.button.Button);
        break;
      case SDL_EventType.WindowResized:
        FrambufferResizedCallback(e.window.data1, e.window.data2);
        break;
      case SDL_EventType.WindowRestored:
        break;
      default:
        break;
    }
  }

  private static unsafe void FrambufferResizedCallback(int width, int height) {
    if (width <= 0 || height <= 0) return;
    Logger.Info($"RESISING {width} {height}");
    WindowState.s_Window.FramebufferResized = true;
    WindowState.s_Window.Extent = new DwarfExtent2D((uint)width, (uint)height);
    WindowState.s_Window.OnResizedEvent(null!);
  }

  private static void Log_SDL(SDL_LogCategory category, SDL_LogPriority priority, string description) {
    if (priority >= SDL_LogPriority.Error) {
      Logger.Error($"[{priority}] SDL: {description}");
      throw new Exception(description);
    } else {
      Logger.Info($"[{priority}] SDL: {description}");
    }
  }

  private void OnResizedEvent(EventArgs e) {
    WindowState.s_Window.OnResizedEventDispatcher?.Invoke(this, e);
  }

  public bool WasWindowResized() => FramebufferResized;
  public bool WasWindowMinimalized() => _windowMinimalized;

  public unsafe VkSurfaceKHR CreateSurface(VkInstance instance) {
    VkSurfaceKHR surface;
    return SDL_Vulkan_CreateSurface(SDLWindow, instance, IntPtr.Zero, (ulong**)&surface) == false
      ? throw new Exception("Failed to create SDL Surface")
      : surface;
  }

  public DwarfExtent2D Extent {
    get { return _extent; }
    private set { _extent = value; }
  }
  public Vector2I Size { get; private set; }
  public bool ShouldClose { get; set; } = false;
  public bool FramebufferResized { get; private set; } = false;
  public bool IsMinimalized { get; private set; } = false;
  public event EventHandler? OnResizedEventDispatcher;

  public SDL_Window SDLWindow { get; private set; }
}