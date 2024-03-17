using System.Numerics;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Extensions.Logging;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static void ValidateAnchor(string text, Anchor anchor) {
    var io = ImGui.GetIO();

    switch (anchor) {
      case Anchor.Right:
        ImGui.SetCursorPos(GetMiddleRight(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.Left:
        ImGui.SetCursorPos(GetMiddleLeft(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.Middle:
        ImGui.SetCursorPos(GetCenter(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.Bottom:
        ImGui.SetCursorPos(GetMiddleBottom(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.Top:
        ImGui.SetCursorPos(GetMiddleTop(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.RightTop:
        ImGui.SetCursorPos(GetRightTop(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.RightBottom:
        ImGui.SetCursorPos(GetRightBottom(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.LeftTop:
        ImGui.SetCursorPos(GetLeftTop(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.LeftBottom:
        ImGui.SetCursorPos(GetLeftBottom(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.MiddleTop:
        ImGui.SetCursorPos(GetMiddleTop(io, ImGui.CalcTextSize(text)));
        break;
      case Anchor.MiddleBottom:
        ImGui.SetCursorPos(GetMiddleBottom(io, ImGui.CalcTextSize(text)));
        break;
      default:
        break;
    }
  }

  private static Vector2 GetCenter(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = (io.DisplaySize / 2) - (offset / 2);
    return displaySize;
  }

  private static Vector2 GetMiddleBottom(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X / 2) - (offset.X / 2);
    displaySize.Y = (displaySize.Y) - (offset.Y + 5);
    return displaySize;
  }

  private static Vector2 GetMiddleTop(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X / 2) - (offset.X / 2);
    displaySize.Y = (offset.Y / 2);
    return displaySize;
  }

  private static Vector2 GetLeftBottom(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = 5;
    displaySize.Y = (displaySize.Y) - (offset.Y + 5);
    return displaySize;
  }

  private static Vector2 GetRightBottom(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X) - (offset.X + 5);
    displaySize.Y = (displaySize.Y) - (offset.Y + 5);
    return displaySize;
  }

  private static Vector2 GetRightTop(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X) - (offset.X + 5);
    displaySize.Y = (offset.Y - 5);
    return displaySize;
  }

  private static Vector2 GetLeftTop(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = 5;
    displaySize.Y = (offset.Y / 2);
    return displaySize;
  }

  private static Vector2 GetMiddleLeft(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = 5;
    displaySize.Y = (displaySize.Y / 2) - (offset.Y / 2);
    return displaySize;
  }

  private static Vector2 GetMiddleRight(ImGuiIOPtr io, Vector2 offset) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X) - (offset.X + 5);
    displaySize.Y = (displaySize.Y / 2) - (offset.Y / 2);
    return displaySize;
  }
}