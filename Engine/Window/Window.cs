using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dwarf.Engine.Math;
// using GLFW;
using Dwarf.Extensions.GLFW;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Windowing;

public unsafe class Window : IDisposable {
  public VkString AppName = new("Dwarf App");
  public VkString EngineName = new("Dwarf Engine");

  private GLFWwindow* _window;
  private VkExtent2D _extent;
  private Vector2I _windowSize;

  // private static bool EnableValidationLayers = true;

  public Window(int width, int height) {
    _windowSize = new Vector2I(width, height);
    InitWindow();
    InitVulkan();
  }

  protected void Clear() {

  }

  private void InitWindow() {
    glfwInit();
    glfwWindowHint((int)WindowHintClientApi.ClientApi, 0);
    glfwWindowHint((int)WindowHintBool.Resizable, 0);
    _window = glfwCreateWindow(_windowSize.X, _windowSize.Y, "Dwarf Vulkan", null, null);
    _extent = new VkExtent2D(_windowSize.X, _windowSize.Y);
  }

  private void InitVulkan() {
    // GraphicsDevice = new GraphicsDevice(this);
  }

  public void Dispose() {
    // GraphicsDevice.Dispose();
    // glfwDestroyWindow(_window);
    glfwTerminate();
  }

  public VkResult CreateSurface(VkInstance instance, VkSurfaceKHR* surface) => glfwCreateWindowSurface(instance, _window, null, surface);

  public bool ShouldClose => glfwWindowShouldClose(_window);

  public VkExtent2D Extent => _extent;

  public Vector2I Size => _windowSize;
}