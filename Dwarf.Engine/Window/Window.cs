using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;

using SDL3;

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

  public Window(int width, int height, string windowName, bool fullscreen) {
    Size = new Vector2I(width, height);
    InitWindow(windowName, fullscreen);
    LoadIcons();
    Show();
  }

  private void InitWindow(string windowName, bool fullscreen) {
    if (SDL_Init(SDL_InitFlags.Video) < 0) {
      throw new Exception("Failed to initalize Window");
    }

    SDL_SetLogOutputFunction(Log_SDL);

    if (SDL_Vulkan_LoadLibrary() < 0) {
      throw new Exception("Failed to initialize Vulkan");
    }

    var windowFlags = SDL_WindowFlags.Vulkan |
                      SDL_WindowFlags.Resizable;

    SDLWindow = SDL_CreateWindow(windowName, Size.X, Size.Y, windowFlags);
    if (SDLWindow.IsNull) {
      throw new Exception("Failed to create SDL window");
    }

    _ = SDL_SetWindowPosition(SDLWindow, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);

    WindowState.s_Window = this;
    WindowState.s_Window.Extent = new DwarfExtent2D((uint)Size.X, (uint)Size.Y);
  }

  private void LoadIcons() {

  }

  public void Terminate() {

  }

  public void Show() {
    SDL_ShowWindow(SDLWindow);
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
        MouseState.MouseCallback(e.motion.xrel, e.motion.yrel);
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

  private static unsafe void IconifyCallback(int iconified) {
    WindowState.s_Window.IsMinimalized = iconified != 0;
    Logger.Info($"Window Minimalized: {WindowState.s_Window.IsMinimalized}");
    if (!WindowState.s_Window.IsMinimalized) {
      // Application.Instance.Renderer.RecreateSwapchain();
    }
  }

  //[UnmanagedCallersOnly]
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
    return SDL_Vulkan_CreateSurface(SDLWindow, instance, (nint)IntPtr.Zero, (ulong**)&surface) != 0
      ? throw new Exception("Failed to create SDL Surface")
      : surface;
  }

  public DwarfExtent2D Extent {
    get { return _extent; }
    private set { _extent = value; }
  }
  public Vector2I Size { get; }
  public bool ShouldClose { get; set; } = false;
  public bool FramebufferResized { get; private set; } = false;
  public bool IsMinimalized { get; private set; } = false;
  public event EventHandler? OnResizedEventDispatcher;

  public SDL_Window SDLWindow { get; private set; }
}

/*
public unsafe class Window_Old : IDisposable {
  public VkUtf8String AppName = "Dwarf App"u8;
  public VkUtf8String EngineName = "Dwarf Engine"u8;
  private DwarfExtent2D _extent;
  private bool _windowMinimalized = false;

  public event EventHandler? OnResizedEventDispatcher;

  private readonly object _windowLock = new();

  public Window(int width, int height, string windowName, bool fullscreen) {
    Size = new Vector2I(width, height);
    InitWindow(windowName, fullscreen);
    LoadIcons();
    LoadGamePadInput();
  }

  public void ResetWindowResizedFlag() {
    FramebufferResized = false;
  }

  public unsafe void SetWindowName(string name) {
    glfwSetWindowTitle(GLFWwindow, name);
  }

  private unsafe void InitWindow(string windowName, bool fullscreen) {
    glfwInit();
    glfwWindowHint((int)WindowHintClientApi.ClientApi, 0);
    glfwWindowHint((int)WindowHintBool.Resizable, 1);
    glfwWindowHint((int)WindowHintBool.Decorated, 0);
    glfwWindowHint((int)WindowHintBool.Floating, 0);
    glfwWindowHint((int)WindowHintBool.DoubleBuffer, 1);

    if (fullscreen) {
      GLFWmonitor = glfwGetPrimaryMonitor();
      GLFWvidmode = glfwGetVideoMode(GLFWmonitor);

      glfwWindowHint((int)WindowHintBool.RedBits, GLFWvidmode->RedBits);
      glfwWindowHint((int)WindowHintBool.GreenBits, GLFWvidmode->GreenBits);
      glfwWindowHint((int)WindowHintBool.BlueBits, GLFWvidmode->BlueBits);
      glfwWindowHint((int)WindowHintBool.RefreshRate, GLFWvidmode->RefreshRate);

      GLFWwindow = glfwCreateWindow(GLFWvidmode->Width, GLFWvidmode->Height, windowName, GLFWmonitor, null);
      _extent = new DwarfExtent2D(GLFWvidmode->Width, GLFWvidmode->Height);
    } else {
      GLFWwindow = glfwCreateWindow(Size.X, Size.Y, windowName, GLFWmonitor, null);
      _extent = new DwarfExtent2D(Size.X, Size.Y);
    }

    // FrambufferResizedCallback(this, _windowSize.X, _windowSize.Y);
    //var w = this;
    //var ptr = GetWindowPtr(&w);
    // glfwSetWindowUserPointer(GLFWwindow, this);
    WindowState.s_Window = this;
    glfwSetFramebufferSizeCallback(GLFWwindow, FrambufferResizedCallback);
    glfwSetCursorPosCallback(GLFWwindow, MouseState.MouseCallback);
    glfwSetScrollCallback(GLFWwindow, MouseState.ScrollCallback);
    glfwSetMouseButtonCallback(GLFWwindow, MouseState.MouseButtonCallback);
    glfwSetKeyCallback(GLFWwindow, KeyboardState.KeyCallback);
    glfwSetWindowIconifyCallback(GLFWwindow, IconifyCallback);

    WindowState.CenterWindow();
    // WindowState.MaximizeWindow();
    WindowState.FocusOnWindow();
    //WindowState.SetCursorMode(InputValue.GLFW_CURSOR_DISABLED);
  }

  private unsafe void LoadIcons() {
    // Load Engine Icon
    var engineIcoStream = File.OpenRead($"{DwarfPath.AssemblyDirectory}/Resources/ico/dwarf_ico.png");
    var engineIco = ImageResult.FromStream(engineIcoStream, ColorComponents.RedGreenBlueAlpha);
    IntPtr engineIcoPtr = Marshal.AllocHGlobal(engineIco.Data.Length * sizeof(char));
    Marshal.Copy(engineIco.Data, 0, engineIcoPtr, engineIco.Data.Length);
    GLFWImage engineImage = new() {
      Width = engineIco.Width,
      Height = engineIco.Height,
      Pixels = (char*)engineIcoPtr
    };
    glfwSetWindowIcon(GLFWwindow, 1, &engineImage);
    Marshal.FreeHGlobal(engineIcoPtr);
    engineIcoStream.Dispose();

    // Load Cursor
    var cursorIcoStream = File.OpenRead($"{DwarfPath.AssemblyDirectory}/Resources/ico/cursor.png");
    var cursorIco = ImageResult.FromStream(cursorIcoStream, ColorComponents.RedGreenBlueAlpha);
    IntPtr cursorPtr = Marshal.AllocHGlobal(cursorIco.Data.Length * sizeof(char));
    Marshal.Copy(cursorIco.Data, 0, cursorPtr, cursorIco.Data.Length);
    GLFWImage cursorImage = new() {
      Width = cursorIco.Width,
      Height = cursorIco.Height,
      Pixels = (char*)cursorPtr
    };
    var cursor = glfwCreateCursor(&cursorImage, 0, 0);
    CursorHandle = new IntPtr(cursor);
    glfwSetCursor(GLFWwindow, (void*)CursorHandle);
    Marshal.FreeHGlobal(cursorPtr);
    cursorIcoStream.Dispose();
  }

  private void LoadGamePadInput() {
    var db = File.ReadAllText($"{DwarfPath.AssemblyDirectory}/Resources/gamecontrollerdb.txt");
    IntPtr mappingsPtr = Marshal.AllocHGlobal(db.Length * sizeof(char));
    Marshal.Copy(db.ToCharArray(), 0, mappingsPtr, db.Length);

    glfwUpdateGamepadMappings((char*)mappingsPtr);
    var name = glfwGetGamepadName(0);
    var isJoystick = glfwJoystickPresent(0);

    int axesCount = 0;

    if (isJoystick == 1) {
      var axes = glfwGetJoystickAxes(0, &axesCount);
    }

    glfwSetJoystickCallback(JoystickState.JoystickCallback);

    Marshal.FreeHGlobal(mappingsPtr);
  }

  private static unsafe void FrambufferResizedCallback(GLFWwindow* window, int width, int height) {
    if (width <= 0 || height <= 0) return;
    Logger.Info($"RESISING {width} {height}");
    WindowState.s_Window.FramebufferResized = true;
    WindowState.s_Window.Extent = new DwarfExtent2D((uint)width, (uint)height);
    WindowState.s_Window.OnResizedEvent(null!);
  }

  private static unsafe void IconifyCallback(GLFWwindow* window, int iconified) {
    WindowState.s_Window.IsMinimalized = iconified != 0;
    Logger.Info($"Window Minimalized: {WindowState.s_Window.IsMinimalized}");
    if (!WindowState.s_Window.IsMinimalized) {
      // Application.Instance.Renderer.RecreateSwapchain();
    }
  }

  private void OnResizedEvent(EventArgs e) {
    WindowState.s_Window.OnResizedEventDispatcher?.Invoke(this, e);
  }

  public void Dispose() {
    // glfwDestroyWindow(_window);
    glfwDestroyCursor((void*)CursorHandle);
    glfwTerminate();
  }

  public bool WasWindowResized() => FramebufferResized;
  public bool WasWindowMinimalized() => _windowMinimalized;

  public VkResult CreateSurface(VkInstance instance, VkSurfaceKHR* surface) {
    return glfwCreateWindowSurface(instance, GLFWwindow, null, surface);
  }

  public bool ShouldClose => glfwWindowShouldClose(GLFWwindow);
  public bool FramebufferResized { get; private set; } = false;

  public bool IsMinimalized {
    get {
      lock (_windowLock) {
        return _windowMinimalized;
      }
    }
    private set {
      lock (_windowLock) {
        _windowMinimalized = value;
      }
    }
  }

  public DwarfExtent2D Extent {
    get { return _extent; }
    private set { _extent = value; }
  }
  public Vector2I Size { get; }
  public GLFWwindow* GLFWwindow { get; private set; }
  public GLFWvidmode* GLFWvidmode { get; private set; }
  public GLFWmonitor* GLFWmonitor { get; private set; }
  public nint CursorHandle { get; private set; }
}
*/