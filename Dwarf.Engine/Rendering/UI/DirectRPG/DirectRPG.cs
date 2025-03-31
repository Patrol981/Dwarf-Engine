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

  public static void BeginParent(string label, ImGuiWindowFlags flags) {
    ImGui.Begin(label, flags);
  }

  public static void EndParent() {
    ImGui.End();
  }

  public static Vector2 DisplaySize => ImGui.GetIO().DisplaySize;
  public static ImGuiController GuiController => Application.Instance.GuiController;

  public static void UploadTexture(ITexture texture) {
    Application.Instance.GuiController.GetOrCreateImGuiBinding((VulkanTexture)texture);
  }

  public static nint GetStoredTexture(string id) {
    var target = Application.Instance.GuiController.StoredTextures.Where(x => x.TextureName == id).First();
    if (target == null) return IntPtr.Zero;
    return Application.Instance.GuiController.GetOrCreateImGuiBinding(target);
  }

  public static nint GetStoredTexture(ITexture texture) {
    return Application.Instance.GuiController.GetOrCreateImGuiBinding((VulkanTexture)texture);
  }

  public static void AlignNextWindow(Anchor anchor, Vector2 size, bool stick = true) {
    //
    // ValidateAnchor(size, anchor);
    SetWindowAlignment(size, anchor, stick);
    // ImGui.SetNextWindowPos(new(0, 0));
  }

  public static void Image(ITexture texture) {
    var winSize = DirectRPG.DisplaySize;
    var aspect = (float)texture.Width / (float)texture.Height;
    var newWidth = winSize.X;
    var newHeight = winSize.X / aspect;
    if (newHeight > winSize.Y) {
      newHeight = winSize.Y;
      newWidth = newHeight * aspect;
    }

    var topLeft = new Vector2((winSize.X - newWidth) * 0.5f, (winSize.Y - newHeight) * 0.5f);
    var bottomRight = new Vector2(topLeft.X + newWidth, topLeft.Y + newHeight);

    ImGui.GetBackgroundDrawList().AddImage(GetStoredTexture(texture), topLeft, bottomRight);
  }
}
