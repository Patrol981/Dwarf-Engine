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

    if (ImGui.IsItemHovered()) {
      var texId = GetStoredTexture(textureHovered);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
    } else {
      var texId = GetStoredTexture(textureStandard);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
    }

    if (ImGui.IsItemClicked()) {
      buttonClicked.Invoke();
    }
  }

  public static void CreateTexturedButton(
    string buttonId,
    ITexture textureStandard,
    ITexture textureHovered,
    Vector2 size,
    Vector2 clickArea,
    Vector2 uv0,
    Vector2 uv1,
    ButtonClickedDelegate buttonClicked
  ) {
    var pos = ImGui.GetCursorScreenPos();

    ImGui.SetCursorScreenPos(pos);
    ImGui.InvisibleButton(buttonId, clickArea);

    if (ImGui.IsItemHovered()) {
      var texId = GetStoredTexture(textureHovered);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, uv0, uv1);
    } else {
      var texId = GetStoredTexture(textureStandard);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, uv0, uv1);
    }

    if (ImGui.IsItemClicked()) {
      buttonClicked.Invoke();
    }
  }

  public static void CreateTexturedButtonWithLabel(
    string buttonId,
    string label,
    ITexture textureStandard,
    ITexture textureHovered,
    Vector2 size,
    Vector2 clickArea,
    float fontSize,
    ButtonClickedDelegate buttonClicked
  ) {
    var pos = ImGui.GetCursorScreenPos();

    ImGui.SetCursorScreenPos(pos);
    ImGui.InvisibleButton(buttonId, clickArea);

    var textLen = ImGui.CalcTextSize(label);
    var textPos = pos + (size - textLen) / 2;
    textPos.X -= fontSize / 2;
    var font = ImGui.GetFont();

    if (ImGui.IsItemHovered()) {
      var texId = GetStoredTexture(textureHovered);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
      textPos.Y -= fontSize / 2 - 5;
      ImGui.GetWindowDrawList().AddText(font, fontSize, textPos, COLOR_WHITE, label);
    } else {
      var texId = GetStoredTexture(textureStandard);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
      textPos.Y -= fontSize / 2;
      ImGui.GetWindowDrawList().AddText(font, fontSize, textPos, COLOR_BLACK, label);
    }

    if (ImGui.IsItemClicked()) {
      buttonClicked.Invoke();
    }

    // ImGui.SetCursorPos(pos + size);
  }
}