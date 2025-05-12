
using System.Numerics;
using Dwarf.AbstractionLayer;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public static void CreateLabel(
    string text,
    FontSize fontSize,
    bool centerX = false,
    bool centerY = false
) {
    // Select font
    switch (fontSize) {
      case FontSize.Small:
        ImGui.PushFont(GuiController.SmallFont);
        break;
      case FontSize.Medium:
        ImGui.PushFont(GuiController.MediumFont);
        break;
      case FontSize.Large:
        ImGui.PushFont(GuiController.LargeFont);
        break;
    }

    // Get text size for layout control
    Vector2 textSize = ImGui.CalcTextSize(text);

    // Center horizontally if requested
    if (centerX) {
      float windowWidth = ImGui.GetWindowSize().X;
      float offsetX = (windowWidth - textSize.X) * 0.5f;
      ImGui.SetCursorPosX(offsetX);
    }

    // Draw manually
    Vector2 drawPos = ImGui.GetCursorScreenPos();
    ImGui.GetWindowDrawList().AddText(drawPos, COLOR_WHITE, text);

    // Use dummy to move cursor vertically after text height
    ImGui.Dummy(new Vector2(textSize.X, textSize.Y));

    ImGui.PopFont();
  }

  public static void CreateLabelWithImage(
    string text,
    FontSize fontSize,
    Vector2 imgSize,
    ITexture texture,
    Vector2 uv0,
    Vector2 uv1,
    bool centerX = false,
    bool centerY = false
  ) {
    ImGui.BeginChild($"{texture.TextureName}-{text}");

    var texId = GetStoredTexture(texture);
    ImGui.Image(texId, imgSize, uv0, uv1);
    ImGui.SameLine();
    CreateLabel(text, fontSize, centerX, centerY);


    ImGui.EndChild();
  }

  public static void CreateLabelWithImage(
    string text,
    FontSize fontSize,
    Vector2 imgSize,
    IndexedImage indexedImage,
    ITexture atlas,
    bool centerX = false,
    bool centerY = false
) {
    // Get UV coordinates from the atlas
    GetUVCoords(
        indexedImage.TextureIndex,
        16,
        16,
        out var uv0,
        out var uv1
    );

    var texId = GetStoredTexture(atlas);

    // Get current cursor screen position for drawing
    Vector2 imagePos = ImGui.GetCursorScreenPos();
    Vector2 imageEnd = imagePos + imgSize;

    // Draw the atlas image using custom UVs
    ImGui.GetWindowDrawList().AddImage(texId, imagePos, imageEnd, uv0, uv1);

    // Move cursor to right of image for label
    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + imgSize.X + 5); // +5px spacing

    // Optional vertical alignment for label
    if (centerY) {
      float textHeight = ImGui.GetFontSize();
      float offsetY = (imgSize.Y - textHeight) * 0.5f;
      ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offsetY);
    }

    // Draw the label
    CreateLabel(text, fontSize, centerX: false, centerY: false); // Y already handled

    // Use Dummy to maintain vertical layout
    ImGui.SetCursorPosY(imagePos.Y + imgSize.Y);
    ImGui.Dummy(new Vector2(1, 1)); // Placeholder to keep spacing
  }

}