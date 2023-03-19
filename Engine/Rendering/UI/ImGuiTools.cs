using ImGuiNET;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;

namespace DwarfEngine.Engine.UI;
public class ImGuiTools {
  public static bool ImGuiVulkanImplementation() {
    return false;
  }

  public static unsafe void ImGuiVulkanImplementationInit(GLFWwindow* window, bool installCallbacks) {
    var io = ImGui.GetIO();
    if (io.BackendPlatformUserData != IntPtr.Zero) {
      Logger.Warn("ImGui - Already initialized a platform backend!");
    }

    // ImGuiIOPtr bd = new();
    IntPtr bd = new();
    // io.BackendPlatformUserData = bd;
    // io.BackendPlatformName = "imgui_impl_glfw";
  }
}
