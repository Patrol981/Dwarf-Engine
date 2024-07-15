using System.Numerics;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;

using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public const ImGuiWindowFlags NONE = ImGuiWindowFlags.None;
  public const ImGuiWindowFlags STATIC_WINDOW = ImGuiWindowFlags.NoTitleBar |
                                                ImGuiWindowFlags.NoResize |
                                                ImGuiWindowFlags.NoCollapse |
                                                ImGuiWindowFlags.NoMove;

  public static void CreateMenuStyles() {
    var colors = ImGui.GetStyle().Colors;
    var style = ImGui.GetStyle();
  }

  public static void BeginParent(string label, Vector2 size, ImGuiWindowFlags flags) {
    ImGui.SetNextWindowSize(size);
    ImGui.Begin(label, flags);
  }

  public static void EndParent() {
    ImGui.End();
  }

  public static void UploadTexture(VulkanTexture texture) {
    Application.Instance.GuiController.GetOrCreateImGuiBinding(texture);
  }

  public static nint GetStoredTexture(string id) {
    var target = Application.Instance.GuiController.StoredTextures.Where(x => x.TextureName == id).First();
    if (target == null) return IntPtr.Zero;
    return Application.Instance.GuiController.GetOrCreateImGuiBinding(target);
  }

  public static void AlignNextWindow(Anchor anchor, Vector2 size, bool stick = true) {
    //
    // ValidateAnchor(size, anchor);
    SetWindowAlignment(size, anchor, stick);
    // ImGui.SetNextWindowPos(new(0, 0));
  }
}
