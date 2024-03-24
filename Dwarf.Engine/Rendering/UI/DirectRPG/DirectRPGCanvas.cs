using System.Numerics;

using Dwarf.Engine;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Utils;
using Dwarf.Vulkan;

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

  public static unsafe void CanvasImage(VulkanTexture texture, Vector2 size, Anchor anchor = Anchor.Middle) {
    var textureDescriptor = texture.GetTextureDescriptor();
    nint descPtr = MemoryUtils.ToIntPtr(textureDescriptor);

    if (textureDescriptor == 0) {
      // texture.BuildDescriptor()
      var controller = Application.Instance.GuiController;
      texture.AddDescriptor(controller.GetDescriptorSetLayout(), controller.GetDescriptorPool());
      var drawData = ImGuiNative.igGetDrawData();
      var drawList = ImGuiNative.igGetWindowDrawList();
      // descPtr = MemoryUtils.ToIntPtr(textureDescriptor);

      // ImGui.GetForegroundDrawList().AddImage(descPtr, new(0, 0), new(500, 500));
      // Marshal.FreeHGlobal(descPtr);

      return;
    }


    // ImGui.GetForegroundDrawList().AddCircle(new(0, 0), 25, 0);
    // ImGui.GetForegroundDrawList().

    var app = Application.Instance;
    var layout = app.GuiController.GetPipelineLayout();
    Descriptor.BindDescriptorSet(
      app.Device,
      texture.GetTextureDescriptor(),
      app.FrameInfo,
      ref layout,
      0,
      1
    );
    // ImGui.GetForegroundDrawList().AddImage(descPtr, new(0, 500), new(500, 0));
    ImGui.ShowMetricsWindow();
    ValidateAnchor("", anchor);
    ImGui.ImageButton("imgbtn", descPtr, new(100, 100));
    // ImGui.Image(MemoryUtils.ToIntPtr(textureDescriptor), new(500, 500));
    // ImGui.Image((nint)textureDescriptor, size);
  }
}