using System.Numerics;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public static void CreateharacterSheet() {
    ImGui.BeginChild("###Character");

    ImGui.BeginChild("###View");
    // ImGui.Image();
    ImGui.EndChild();

    ImGui.EndChild();
  }
}