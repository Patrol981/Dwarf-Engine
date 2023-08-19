using ImGuiNET;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using System.Numerics;

namespace DwarfEngine.Engine.UI;
public class ImGuiTools {

  internal enum GLFWClientApi {
    Unknkown,
    OpenGL,
    Vulkan
  }

  internal unsafe struct ImGuiData {
    public GLFWwindow* WindowPtr;
    public GLFWClientApi ClientApi;
    public double Time;
    public GLFWwindow* MouseWindow;
    public IntPtr CursorPointers;
    Vector2 LastValidMousePos;
  }


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
    io.BackendPlatformUserData = bd;
    // io.BackendPlatformName = "imgui_impl_glfw";
    // var pIo = ImGui.GetPlatformIO();
    // pIo.
    io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;         // We can honor GetMouseCursor() values (optional)
    io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
    // io.BackendPlatformUserData = bd;
    // io.BackendPlatformName = "imgui_impl_glfw";
  }
}
