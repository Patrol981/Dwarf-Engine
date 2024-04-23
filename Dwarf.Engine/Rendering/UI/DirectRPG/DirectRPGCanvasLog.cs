using System.Numerics;

using Dwarf.Rendering.UI;

using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;
public partial class DirectRPG {
  private static IEnumerable<string> s_logMessages = [];

  public static void AddLog(string logMsg) {
    s_logMessages = s_logMessages.Append(logMsg);
  }

  public static void CanvasLog() {
    var size = s_canvasSize;
    size.X /= 4;
    size.Y /= 5;
    CanvasLogBase(size);
  }

  public static void CanvasLog(Vector2 size) {
    CanvasLogBase(size);
  }

  private static void CanvasLogBase(Vector2 size) {
    ImGui.SetNextWindowSize(size);
    size.X -= 5;
    size.Y -= 5;
    SetWindowAlignment(size, Anchor.RightBottom);
    ImGui.Begin("Canvas Log");
    using var seq = s_logMessages.GetEnumerator();
    while (seq.MoveNext()) {
      ImGui.TextWrapped(seq.Current);
    }
    ImGui.End();
  }
}
