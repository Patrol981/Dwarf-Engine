using System.Numerics;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Extensions.Logging;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public delegate void ButtonClickedDelegate();
  public static void BeginCanvas() {
    var io = ImGui.GetIO();

    ImGui.SetNextWindowPos(new(0, 0));
    ImGui.SetNextWindowSize(io.DisplaySize);
    ImGui.SetNextWindowBgAlpha(0.0f);
    ImGui.Begin("Canvas",
      ImGuiWindowFlags.NoDecoration |
      ImGuiWindowFlags.NoMove |
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoBringToFrontOnFocus
    );
  }

  public static void EndCanvas() {
    ImGui.End();
  }

  public static void CanvasText(string text, Anchor anchor = Anchor.Middle) {
    ValidateAnchor(text, anchor);
    ImGui.Text(text);
  }

  public static void CanvasButton(string label, ButtonClickedDelegate buttonClicked, Anchor anchor = Anchor.Middle) {
    ValidateAnchor(label, anchor);
    if (ImGui.Button(label)) {
      buttonClicked.Invoke();
    }
  }
}