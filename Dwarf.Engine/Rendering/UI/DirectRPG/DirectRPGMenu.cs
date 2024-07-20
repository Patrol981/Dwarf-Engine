using System.Numerics;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static float s_menuOffset = 0;

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

  public static void CreateMenuButton(
    string label,
    ButtonClickedDelegate buttonClicked,
    Vector2 size = default,
    bool center = true
  ) {
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
      buttonClicked.Invoke();
    }

    s_menuOffset += size.Y;
  }

  public static void CreateMenuText(string label, bool center = true) {
    var io = ImGui.GetIO();
    var len = ImGui.CalcTextSize(label);

    if (center) {
      var centerPos = io.DisplaySize / 2;
      centerPos.X -= len.X / 2;
      centerPos.Y -= (len.Y / 2) - s_menuOffset * 2;
      ImGui.SetCursorPos(centerPos);
    }
    ImGui.Text(label);

    s_menuOffset += len.Y * 2;
  }
}