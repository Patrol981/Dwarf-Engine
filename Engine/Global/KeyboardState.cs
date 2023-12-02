using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Testing;
using Dwarf.Extensions.GLFW;
using Dwarf.Vulkan;

namespace Dwarf.Engine.Globals;

public class KeyState {
  public bool KeyDown { get; set; }
  public bool KeyPressed { get; set; }

  public KeyState() {
    KeyDown = false;
    KeyPressed = false;
  }
}

public sealed class KeyboardState {
  private static KeyboardState s_instance = null!;
  private static bool s_debug = true;

  private Dictionary<int, KeyState> _keyStates = [];

  public KeyboardState() {
    foreach (var enumValue in Enum.GetValues(typeof(Keys))) {
      _keyStates.TryAdd((int)enumValue, new());
    }
  }

  public static unsafe void KeyCallback(GLFWwindow* window, int key, int scancode, int action, int mods) {
    switch (action) {
      case (int)Dwarf.KeyAction.GLFW_PRESS:
        s_instance._keyStates[key].KeyDown = true;
        s_instance._keyStates[key].KeyPressed = true;

        if (key == (int)Dwarf.Keys.GLFW_KEY_F) WindowState.FocusOnWindow();
        if (key == (int)Dwarf.Keys.GLFW_KEY_F1) WindowState.MaximizeWindow();
        if (key == (int)Dwarf.Keys.GLFW_KEY_GRAVE_ACCENT) ChangeWireframeMode();
        if (key == (int)Dwarf.Keys.GLFW_KEY_1) ChangeDebugVisiblity();
        break;
      case (int)KeyAction.GLFW_REPEAT:
        break;
      case (int)KeyAction.GLFW_RELEASE:
        s_instance._keyStates[key].KeyDown = false;
        break;
      default:
        break;
    }
    // _currentKeyPressed = (int)Keys.GLFW_KEY_UNKNOWN;
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

  private static KeyboardState GetInstance() {
    if (s_instance == null) {
      s_instance = new KeyboardState();
    }
    return s_instance;
  }

  public Dictionary<int, KeyState> KeyStates => _keyStates;

  public static KeyboardState Instance => GetInstance();
}