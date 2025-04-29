using Dwarf.AbstractionLayer;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static float s_RTS_Rows = 0.0f;
  private static float s_RTS_Cols = 0.0f;

  private static ITexture? s_MainBg;

  public static void CreateRTSTheme(Application app) {
    s_MainBg = CreateTexture(app, "./Resources/UI/Banners/Carved_Regular.png");
  }

  public static void CreateBottomRTSPanel() {
    var pos = ImGui.GetIO().MousePos;
    ImGui.SetNextWindowPos(pos);
    ImGui.Begin("RTS_Panel", ImGuiWindowFlags.NoDecoration |
      ImGuiWindowFlags.NoBringToFrontOnFocus);

    if (s_MainBg == null) return;

    StickyImage(s_MainBg, pos);
  }

  public static void EndBottomRTSPanel() {
    ImGui.End();
  }
}