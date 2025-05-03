using System.Numerics;
using Dwarf.AbstractionLayer;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public static void CreateTexturedPanel(
    ITexture texture,
    Vector2 size
  ) {
    var texId = GetStoredTexture(texture);
    var pos = ImGui.GetCursorScreenPos();

    ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
  }
}