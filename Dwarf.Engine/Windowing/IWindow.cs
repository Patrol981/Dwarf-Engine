using Dwarf.Math;

namespace Dwarf.Windowing;

public interface IWindow : IDisposable {
  DwarfExtent2D Extent { get; }
  bool ShouldClose { get; set; }
  bool FramebufferResized { get; }
  bool IsMinimalized { get; }
  event EventHandler? OnResizedEventDispatcher;
  float RefreshRate { get; }

  void Show();
  void PollEvents();
  void WaitEvents();
  void EnumerateAvailableGameControllers();

  float GetRefreshRate();
}