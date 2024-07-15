using System.Numerics;
using Dwarf.Extensions.Logging;
using ImGuiNET;
using OpenTK.Graphics.ES11;

namespace Dwarf.Rendering.UI.DirectRPG;

public struct SpellBarItem {
  public string TextureId;
}

public partial class DirectRPG {
  public static Vector2 SpellBarSize = new(470, 105);

  private static int s_spellBarSlotLength = 40;
  private static int s_spellBarItemsPerRow = 10;
  private static Vector2 s_spellItemSize = new(32, 32);
  private static SpellBarItem[] s_spellBarItems = new SpellBarItem[s_spellBarSlotLength];

  public static async void SetSpellItems(SpellBarItem[] spellBarItems) {
    var rowsApprox = MathF.Ceiling(spellBarItems.Length / 10f);
    s_spellBarSlotLength = (int)rowsApprox * 10;
    s_spellBarItems = new SpellBarItem[s_spellBarSlotLength];

    // size = (buttonSize * numOfRows)
    var sizeToUpdate = 105 * (rowsApprox - 1);
    SpellBarSize.Y += sizeToUpdate;

    for (int i = 0; i < s_spellBarSlotLength; i++) {
      s_spellBarItems[i] = spellBarItems.Length > i ? spellBarItems[i] : new SpellBarItem();
    }

    var app = Application.Instance;
    await app.TextureManager.AddTexture("./Resources/ico/dwarf_ico.png");
    var textureId = app.TextureManager.GetTextureId("./Resources/ico/dwarf_ico.png");
    var texture = (VulkanTexture)app.TextureManager.GetTexture(textureId);
    UploadTexture(texture);
  }

  public static void CreateSpellBar() {
    ImGui.BeginChild("###SpellBar");
    int x = 0;

    for (int i = 0; i < s_spellBarSlotLength; i++) {
      nint imTex;
      if (s_spellBarItems[i].TextureId != String.Empty && s_spellBarItems[i].TextureId != null) {
        imTex = GetStoredTexture(s_spellBarItems[i].TextureId);
      } else {
        imTex = GetStoredTexture("./Resources/ico/dwarf_ico.png");
      }
      if (ImGui.ImageButton($"{i}", imTex, s_spellItemSize)) {

      }

      if (ImGui.IsItemHovered()) {
        ImGui.BeginTooltip();
        ImGui.Text($"{i}");
        // ImGui.Text(s_items[i].ItemDesc);
        ImGui.EndTooltip();
      }

      x++;
      if (x < s_spellBarItemsPerRow) {
        ImGui.SameLine();
      } else {
        x = 0;
        ImGui.NewLine();
      }
    }
    ImGui.EndChild();
  }
}