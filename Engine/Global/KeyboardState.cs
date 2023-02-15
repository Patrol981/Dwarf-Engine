using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Loaders;
using Dwarf.Extensions.GLFW;
using OpenTK.Mathematics;

namespace Dwarf.Engine.Globals;

public sealed class KeyboardState {
  private static KeyboardState s_instance = null!;

  public static unsafe void KeyCallback(GLFWwindow* window, int key, int scancode, int action, int mods) {
    // Console.WriteLine(key);
    switch (action) {
      case (int)GLFWKeyMap.KeyAction.GLFW_PRESS:
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_F) WindowState.FocusOnWindow();
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_F1) WindowState.MaximizeWindow();
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_ENTER) AddEntity();
        if (key == (int)GLFWKeyMap.Keys.GLFW_KEY_RIGHT_SHIFT) RemoveEntity();
        break;
    }
  }

  static void AddEntity() {
    var box2 = new Entity();
    box2.AddComponent(new GenericLoader().LoadModel(ApplicationState.s_App.Device, "./Models/colored_cube.obj"));
    box2.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    box2.AddComponent(new Transform(new Vector3(1.0f, -3.0f, -1f)));
    box2.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
    box2.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
    ApplicationState.s_App.AddEntity(box2);
  }

  static void AddEntityRange(int range) {
    for (int i = 0; i < range; i++) {
      var box2 = new Entity();
      box2.AddComponent(new GenericLoader().LoadModel(ApplicationState.s_App.Device, "./Models/colored_cube.obj"));
      box2.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
      box2.AddComponent(new Transform(new Vector3(i, -3.0f, -i * 2)));
      box2.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
      box2.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
      ApplicationState.s_App.AddEntity(box2);
    }
  }

  static void RemoveEntity() {
    //if (_renderer.IsFrameStarted || _renderer.IsFrameInProgress) return;
    var count = ApplicationState.s_App.GetEntities().Count - 1;
    var entities = ApplicationState.s_App.GetEntities();
    entities[count].GetComponent<Model>().CanBeDisposed = true;
    //entities[count].GetComponent<Model>()?.Dispose();
    //ApplicationState.s_App.RemoveEntityAt(count);
  }

  static void RemoveEntityRange(int range) {
    var entities = ApplicationState.s_App.GetEntities();
    entities.Reverse();
    for (int i = 0; i < range; i++) {
      entities[i].GetComponent<Model>().CanBeDisposed = true;
      // entities[i].GetComponent<Model>()?.Dispose();
    }
    // ApplicationState.s_App.RemoveEntityRange(count, range);
  }

  public KeyboardState GetInstance() {
    if (s_instance == null) {
      s_instance = new KeyboardState();
    }
    return s_instance;
  }
}