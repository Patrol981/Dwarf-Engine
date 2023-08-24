using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Testing;
using Dwarf.Extensions.GLFW;
using Dwarf.Vulkan;

using OpenTK.Mathematics;

namespace Dwarf.Engine.Globals;

public sealed class KeyboardState {
  private static KeyboardState s_instance = null!;

  private static bool s_debug = true;

  public static unsafe void KeyCallback(GLFWwindow* window, int key, int scancode, int action, int mods) {
    // Console.WriteLine(key);
    switch (action) {
      case (int)GLFWKeyMap.KeyAction.GLFW_PRESS:
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_F) WindowState.FocusOnWindow();
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_F1) WindowState.MaximizeWindow();
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_GRAVE_ACCENT) ChangeWireframeMode();
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_1) ChangeDebugVisiblity();
        break;
    }
    PerformanceTester.KeyHandler(action, key);
  }

  static void ChangeWireframeMode() {
    if (ApplicationState.Instance.CurrentPipelineConfig.GetType() == typeof(PipelineConfigInfo)) {
      ApplicationState.Instance.CurrentPipelineConfig = new VertexDebugPipeline();
    } else {
      ApplicationState.Instance.CurrentPipelineConfig = new PipelineConfigInfo();
    }
    ApplicationState.Instance.GetSystems().Reload3DRenderSystem = true;
    ApplicationState.Instance.GetSystems().Reload2DRenderSystem = true;
    // ApplicationState.Instance.GetSystems().ReloadUISystem = true;
  }

  static void ChangeDebugVisiblity() {
    s_debug = !s_debug;
    var entities = ApplicationState.Instance.GetEntities();
    var debugObjects = Entity.DistinctInterface<IDebugRender3DObject>(entities);
    foreach (var entity in debugObjects) {
      var e = entity.GetDrawable<IDebugRender3DObject>() as IDebugRender3DObject;
      if (s_debug) {
        e?.Enable();
      } else {
        e?.Disable();
      }
    }
  }

  public KeyboardState GetInstance() {
    if (s_instance == null) {
      s_instance = new KeyboardState();
    }
    return s_instance;
  }
}