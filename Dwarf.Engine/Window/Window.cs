using System.Runtime.InteropServices;

using Dwarf.Engine.Globals;
using Dwarf.Engine.Math;
using Dwarf.Extensions.Logging;
using Dwarf.GLFW;
using Dwarf.GLFW.Core;
using Dwarf.Utils;

using StbImageSharp;

using Vortice.Vulkan;

using static Dwarf.GLFW.GLFW;

// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;

namespace Dwarf.Engine.Windowing;

public unsafe class Window : IDisposable {
  public VkString AppName = new("Dwarf App");
  public VkString EngineName = new("Dwarf Engine");
  private DwarfExtent2D _extent;
  private bool _windowMinimalized = false;

  public event EventHandler OnResizedEventDispatcher;

  private readonly object _windowLock = new();

  // private static bool EnableValidationLayers = true;

  public Window(int width, int height, string windowName) {
    Size = new Vector2I(width, height);
    InitWindow(windowName);
    LoadIcons();
    LoadGamePadInput();
  }

  public void ResetWindowResizedFlag() {
    FramebufferResized = false;
  }

  public unsafe void SetWindowName(string name) {
    glfwSetWindowTitle(GLFWwindow, name);
  }

  protected void Clear() {

  }

  private unsafe void InitWindow(string windowName) {
    glfwInit();
    glfwWindowHint((int)WindowHintClientApi.ClientApi, 0);
    glfwWindowHint((int)WindowHintBool.Resizable, 1);
    GLFWwindow = glfwCreateWindow(Size.X, Size.Y, windowName, null, null);
    _extent = new DwarfExtent2D(Size.X, Size.Y);

    // FrambufferResizedCallback(this, _windowSize.X, _windowSize.Y);
    //var w = this;
    //var ptr = GetWindowPtr(&w);
    //glfwSetWindowUserPointer(_window, ptr);
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
    WindowState.s_Window._windowMinimalized = iconified != 0;
    Logger.Info($"Window Minimalized: {WindowState.s_Window._windowMinimalized}");
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
  public nint CursorHandle { get; private set; }
}