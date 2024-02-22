using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Dwarf.Engine.Global;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Math;

using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using StbImageSharp;

using Vortice.Vulkan;

using static System.Net.Mime.MediaTypeNames;

using static Vortice.Vulkan.Vulkan;

using Dwarf.GLFW;
using Dwarf.GLFW.Core;
using static Dwarf.GLFW.GLFW;
using Dwarf.Utils;

// using Dwarf.Extensions.GLFW;
// using static Dwarf.Extensions.GLFW.GLFW;

namespace Dwarf.Engine.Windowing;

public unsafe class Window : IDisposable {
  public VkString AppName = new("Dwarf App");
  public VkString EngineName = new("Dwarf Engine");

  private GLFWwindow* _window;
  private IntPtr _cursor;
  private DwarfExtent2D _extent;
  private Vector2I _windowSize;
  private bool _frambufferWindowResized = false;

  public event EventHandler OnResizedEventDispatcher;

  // private static bool EnableValidationLayers = true;

  public Window(int width, int height, string windowName) {
    _windowSize = new Vector2I(width, height);
    InitWindow(windowName);
    LoadIcons();
    LoadGamePadInput();
  }

  public void ResetWindowResizedFlag() {
    _frambufferWindowResized = false;
  }

  public unsafe void SetWindowName(string name) {
    glfwSetWindowTitle(_window, name);
  }

  protected void Clear() {

  }

  private unsafe void InitWindow(string windowName) {
    glfwInit();
    glfwWindowHint((int)WindowHintClientApi.ClientApi, 0);
    glfwWindowHint((int)WindowHintBool.Resizable, 1);
    _window = glfwCreateWindow(_windowSize.X, _windowSize.Y, windowName, null, null);
    _extent = new DwarfExtent2D(_windowSize.X, _windowSize.Y);

    // FrambufferResizedCallback(this, _windowSize.X, _windowSize.Y);
    //var w = this;
    //var ptr = GetWindowPtr(&w);
    //glfwSetWindowUserPointer(_window, ptr);
    WindowState.s_Window = this;
    glfwSetFramebufferSizeCallback(_window, FrambufferResizedCallback);
    glfwSetCursorPosCallback(_window, MouseState.MouseCallback);
    glfwSetScrollCallback(_window, MouseState.ScrollCallback);
    glfwSetMouseButtonCallback(_window, MouseState.MouseButtonCallback);
    glfwSetKeyCallback(_window, KeyboardState.KeyCallback);

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
    glfwSetWindowIcon(_window, 1, &engineImage);
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
    _cursor = new IntPtr(cursor);
    glfwSetCursor(_window, (void*)_cursor);
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

  private void OnResizedEvent(EventArgs e) {
    WindowState.s_Window.OnResizedEventDispatcher?.Invoke(this, e);
  }

  public void Dispose() {
    // glfwDestroyWindow(_window);
    glfwDestroyCursor((void*)_cursor);
    glfwTerminate();
  }

  public bool WasWindowResized() => _frambufferWindowResized;

  public VkResult CreateSurface(VkInstance instance, VkSurfaceKHR* surface) {
    return glfwCreateWindowSurface(instance, _window, null, surface);
  }

  public bool ShouldClose => glfwWindowShouldClose(_window);
  public bool FramebufferResized {
    get { return _frambufferWindowResized; }
    set { _frambufferWindowResized = value; }
  }

  public DwarfExtent2D Extent {
    get { return _extent; }
    private set { _extent = value; }
  }
  public Vector2I Size => _windowSize;
  public GLFWwindow* GLFWwindow => _window;
  public nint CursorHandle => _cursor;
}