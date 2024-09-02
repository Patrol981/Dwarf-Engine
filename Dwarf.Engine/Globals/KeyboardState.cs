using Dwarf.EntityComponentSystem;
// using Dwarf.GLFW.Core;
using Dwarf.Physics;
using Dwarf.Testing;
// using Dwarf.Extensions.GLFW;
using Dwarf.Vulkan;

using SDL3;

namespace Dwarf.Globals;

public class KeyState {
  public bool KeyDown { get; set; }
  public bool KeyPressed { get; set; }

  public KeyState() {
    KeyDown = false;
    KeyPressed = false;
  }
}

public sealed class KeyboardState {
  private static KeyboardState s_instance = GetInstance();
  private static bool s_debug = true;

  public KeyboardState() {
    foreach (var enumValue in Enum.GetValues(typeof(SDL_Keycode))) {
      KeyStates.TryAdd((int)(uint)enumValue, new());
    }
  }

  public static void KeyCallback(SDL_Window window, SDL_KeyboardEvent e, SDL_EventType a) {
    switch (a) {
      case SDL_EventType.KeyDown:
        s_instance.KeyStates[(int)e.key].KeyDown = true;
        s_instance.KeyStates[(int)e.key].KeyPressed = true;

        if (e.key == SDL_Keycode.F) WindowState.FocusOnWindow();
        if (e.key == SDL_Keycode.F1) WindowState.MaximizeWindow();
        if (e.key == SDL_Keycode.Grave) ChangeWireframeMode();
        if (e.key == SDL_Keycode._1) ChangeDebugVisiblity();

        break;
      case SDL_EventType.KeyUp:
        s_instance.KeyStates[(int)e.key].KeyDown = false;
        break;
      default:
        break;
    }
  }

  public static unsafe void KeyCallback(SDL_Window window, int key, int scancode, int action, int mods) {
    switch (action) {
      case (int)KeyAction.GLFW_PRESS:
        if (s_instance.KeyStates.ContainsKey(key)) {
          s_instance.KeyStates[key].KeyDown = true;
          s_instance.KeyStates[key].KeyPressed = true;
        }

        if (key == (int)Keys.GLFW_KEY_F) WindowState.FocusOnWindow();
        if (key == (int)Keys.GLFW_KEY_F1) WindowState.MaximizeWindow();
        if (key == (int)Keys.GLFW_KEY_GRAVE_ACCENT) ChangeWireframeMode();
        if (key == (int)Keys.GLFW_KEY_1) ChangeDebugVisiblity();
        break;
      case (int)KeyAction.GLFW_REPEAT:
        break;
      case (int)KeyAction.GLFW_RELEASE:
        if (s_instance.KeyStates.ContainsKey(key)) {
          s_instance.KeyStates[key].KeyDown = false;
        }
        break;
      default:
        break;
    }
    PerformanceTester.KeyHandler(action, key);
  }

  static void ChangeWireframeMode() {
    Application.Instance.CurrentPipelineConfig = Application.Instance.CurrentPipelineConfig.GetType() == typeof(PipelineConfigInfo)
      ? new VertexDebugPipeline()
      : new PipelineConfigInfo();
    Application.Instance.Systems.Reload3DRenderSystem = true;
    Application.Instance.Systems.Reload2DRenderSystem = true;
  }

  static void ChangeDebugVisiblity() {
    s_debug = !s_debug;
    var entities = Application.Instance.GetEntities();
    var debugObjects = entities.DistinctInterface<IDebugRender3DObject>();
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
    s_instance ??= new KeyboardState();
    return s_instance;
  }

  public Dictionary<int, KeyState> KeyStates { get; } = [];

  public static KeyboardState Instance => GetInstance();
}