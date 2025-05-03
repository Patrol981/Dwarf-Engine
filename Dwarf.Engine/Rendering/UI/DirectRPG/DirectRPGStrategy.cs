using System.Numerics;
using Dwarf.AbstractionLayer;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static float s_RTS_Rows = 0.0f;
  private static float s_RTS_Cols = 0.0f;

  private static ITexture? s_MainBg;

  public static void CreateRTSTheme(Application app) {
    s_MainBg = CreateTexture(app, "./Resources/UI/Banners/Carved_9Slides.png");
  }

  public static void CreateBottomRTSPanel() {
    var size = new Vector2(DisplaySize.X, 200);

    ImGui.SetNextWindowSize(size);
    ImGui.SetNextWindowPos(new(0, DisplaySize.Y - size.Y));
    ImGui.Begin("RTS_Panel", ImGuiWindowFlags.NoDecoration |
                             ImGuiWindowFlags.NoBringToFrontOnFocus |
                             ImGuiWindowFlags.NoMove
    );

    if (s_MainBg == null) return;

    // CreateTexturedPanel(s_MainBg, size);
  }

  public static void EndBottomRTSPanel() {
    ImGui.End();
  }

  public static void CreateGrid(int cols, int rows) {
    for (int y = 0; y < cols; y++) {
      for (int x = 0; x < rows; x++) {
        ImGui.SameLine();
      }

      ImGui.NewLine();
    }
  }
}