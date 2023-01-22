using System.Net.Mime;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public unsafe class Application {
  public delegate void EventCallback();

  public void SetUpdateCallback(EventCallback eventCallback) {
    _onUpdate = eventCallback;
  }

  public void SetRenderCallback(EventCallback eventCallback) {
    _onRender = eventCallback;
  }

  public void SetGUICallback(EventCallback eventCallback) {
    _onGUI = eventCallback;
  }

  public void SetOnLoadCallback(EventCallback eventCallback) {
    _onLoad = eventCallback;
  }

  private EventCallback? _onUpdate;
  private EventCallback? _onRender;
  private EventCallback? _onGUI;
  private EventCallback? _onLoad;

  private Window _window = null!;
  private Device _device = null!;
  private Renderer _renderer = null!;
  private SimpleRenderSystem _simpleRender = null!;
  private List<Entity> _entities = new();

  // private Model _model;
  // private Entity _testEntity;

  public Application() {
    _window = new Window(1200, 900);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);
    _simpleRender = new(_device, _renderer.GetSwapchainRenderPass());
    LoadEntities();
    Run();
  }

  public void Run() {
    while (!_window.ShouldClose) {
      glfwPollEvents();

      var commandBuffer = _renderer.BeginFrame();
      if (commandBuffer != VkCommandBuffer.Null) {
        _renderer.BeginSwapchainRenderPass(commandBuffer);
        _simpleRender.RenderEntities(commandBuffer, _entities.ToArray());
        _renderer.EndSwapchainRenderPass(commandBuffer);
        _renderer.EndFrame();
      }
    }

    var result = vkDeviceWaitIdle(_device.LogicalDevice);
    if (result != VkResult.Success) {
      Logger.Error(result.ToString());
    }
    Cleanup();
  }

  public void AddEntity(Entity entity) {
    _entities.Add(entity);
  }

  private void Init() {
    LoadEntities();
  }

  private void LoadEntities() {
    Vertex[] vertices = new Vertex[3];
    vertices[0] = new();
    vertices[1] = new();
    vertices[2] = new();

    vertices[0].Position = new System.Numerics.Vector2(0.0f, -0.5f);
    vertices[1].Position = new System.Numerics.Vector2(0.5f, 0.5f);
    vertices[2].Position = new System.Numerics.Vector2(-0.5f, 0.5f);

    vertices[0].Color = new Vector3(1.0f, 0.0f, 0.0f);
    vertices[1].Color = new Vector3(0.0f, 1.0f, 0.0f);
    vertices[2].Color = new Vector3(0.0f, 0.0f, 1.0f);

    var en = new Entity();
    en.AddComponent(new Model(_device, vertices));
    en.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    en.AddComponent(new Transform2D(new Vector2(0.2f, 0.0f)));
    AddEntity(en);

    var en2 = new Entity();
    en2.AddComponent(new Model(_device, vertices));
    en2.AddComponent(new Material(new Vector3(0.0f, 0.0f, 0.1f)));
    en2.AddComponent(new Transform2D(new Vector2(0f, 0.0f)));
    AddEntity(en2);

    var en3 = new Entity();
    en3.AddComponent(new Model(_device, vertices));
    en3.AddComponent(new Material(new Vector3(0.1f, 0.0f, 0.0f)));
    en3.AddComponent(new Transform2D(new Vector2(-0.2f, 0.2f), new Vector3(1, 1.3f, 1)));
    en3.GetComponent<Transform2D>().Rotation = 0.25f * (MathF.PI * 2);
    AddEntity(en3);
  }

  private void Cleanup() {
    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      entities[i].GetComponent<Model>()?.Dispose();
    }
    _renderer?.Dispose();
    _simpleRender?.Dispose();
    _window?.Dispose();
    _device?.Dispose();
  }
}