using System.Numerics;

using Dwarf.Extensions.Logging;

using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;
public partial class DirectRPG {
  private static float s_menuOffset = 0;

  public static void CreateMenuStyles() {
    var colors = ImGui.GetStyle().Colors;
    var style = ImGui.GetStyle();
  }

  public static void BeginMainMenu() {
    CreateMenuStyles();
    var io = ImGui.GetIO();
    ImGui.SetNextWindowPos(new(0, 0));
    ImGui.SetNextWindowSize(io.DisplaySize);
    ImGui.SetNextWindowBgAlpha(0.5f);
    ImGui.Begin("Fullscreen Menu",
      ImGuiWindowFlags.NoDecoration |
      ImGuiWindowFlags.NoMove |
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoBringToFrontOnFocus
    );
  }

  public static void EndMainMenu() {
    ImGui.End();
    s_menuOffset = 0;
  }

  public static void CreateMenuButton(string label, Vector2 size = default, bool center = true) {
    var io = ImGui.GetIO();

    if (size == default) {
      size.X = 200;
      size.Y = 50;
    }
    if (center) {
      var centerPos = io.DisplaySize / 2;
      centerPos.X -= size.X / 2;
      centerPos.Y -= (size.Y / 2) - s_menuOffset * 2;
      ImGui.SetCursorPos(centerPos);
    }
    if (ImGui.Button(label, size)) {
      Logger.Info("Test");
    }

    s_menuOffset += size.Y;
  }

  public static void CreateInventory() {
    ImGui.BeginChild("Inventory");

    ImGui.Text("Test");

    ImGui.EndChild();
  }
}
