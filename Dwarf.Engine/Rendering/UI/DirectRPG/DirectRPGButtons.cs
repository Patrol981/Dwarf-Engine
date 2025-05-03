using System.Numerics;
using Dwarf.AbstractionLayer;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public static void CreateTexturedButton(
    string buttonId,
    ITexture textureStandard,
    ITexture textureHovered,
    Vector2 size,
    Vector2 clickArea,
    ButtonClickedDelegate buttonClicked
  ) {
    var pos = ImGui.GetCursorScreenPos();

    ImGui.SetCursorScreenPos(pos);
    ImGui.InvisibleButton(buttonId, clickArea);

    if(ImGui.IsItemHovered()) {
      var texId = GetStoredTexture(textureHovered);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
    } else {
      var texId = GetStoredTexture(textureStandard);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
    }

    if(ImGui.IsItemClicked()) {
      buttonClicked.Invoke();
    }
  }
}