using Dwarf.Math;
using Vortice.Vulkan;

namespace Dwarf.Windowing;

public interface IWindow : IDisposable {
  DwarfExtent2D Extent { get; set; }
  bool ShouldClose { get; set; }
  bool FramebufferResized { get; set; }
  bool IsMinimalized { get; }
  public bool WasWindowResized();
  public bool WasWindowMinimalized();
  void OnResizedEvent(EventArgs e);
  event EventHandler? OnResizedEventDispatcher;
  float RefreshRate { get; }

  void Show();
  void PollEvents();
  void WaitEvents();
  void EnumerateAvailableGameControllers();
  void Init(string windowName, bool fullscreen, int width, int height, bool debug);

  void SetCursorMode(CursorState cursorState);
  void FocusOnWindow();
  void MaximizeWindow();
  void ResetWindowResizedFlag();

  float GetRefreshRate();

  VkSurfaceKHR CreateVkSurface(VkInstance instance);
}