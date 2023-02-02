using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Rendering;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using OpenTK.Mathematics;
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
  private Camera _camera;

  // private Model _model;
  // private Entity _testEntity;

  public Application() {
    _window = new Window(1200, 900);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);
    _simpleRender = new(_device, _renderer.GetSwapchainRenderPass());
    _camera = new();

    LoadEntities();
    Run();
  }

  public void Run() {
    while (!_window.ShouldClose) {
      glfwPollEvents();
      float aspect = _renderer.AspectRatio;
      //_camera.SetOrthograpicProjection(-aspect, aspect, -1, 1, -1f, 1f);
      //_camera.SetPerspectiveProjection(-aspect, aspect, -1, 1, -1f, 1f);
      //_camera.SetViewDirection(new Vector3(0, 0, -2), new Vector3(0, 0, 0), Vector3.Zero);
      // _camera.SetPerspectiveProjection(MathHelper.DegreesToRadians(50f), aspect, 0.01f, 100f);
      _camera.SetPerspectiveProjection(50, aspect, 0.01f, 100f);


      // _camera.SetViewTarget(new Vector3(-1, 0, 0), new Vector3(0, 0, 0), Vector3.Zero);

      var commandBuffer = _renderer.BeginFrame();
      if (commandBuffer != VkCommandBuffer.Null) {
        _renderer.BeginSwapchainRenderPass(commandBuffer);
        _simpleRender.RenderEntities(commandBuffer, _entities.ToArray(), _camera);
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

  private Model GetCube(Vector3 offset) {
    List<Vertex> m = new();
    Vertex v = new();
    // start

    v = new();
    v.Position = new(-.5f, -.5f, -.5f);
    v.Color = new(.9f, .9f, .9f);
    m.Add(v); // 1

    v = new();
    v.Position = new(-.5f, .5f, .5f);
    v.Color = new(.9f, .9f, .9f);
    m.Add(v); // 2

    v = new();
    v.Position = new(-.5f, -.5f, .5f);
    v.Color = new(.9f, .9f, .9f);
    m.Add(v); // 3

    v = new();
    v.Position = new(-.5f, -.5f, -.5f);
    v.Color = new(.9f, .9f, .9f);
    m.Add(v); // 4

    v = new();
    v.Position = new(-.5f, .5f, -.5f);
    v.Color = new(.9f, .9f, .9f);
    m.Add(v); // 5

    v = new();
    v.Position = new(-.5f, .5f, .5f);
    v.Color = new(.9f, .9f, .9f);
    m.Add(v); // 6

    // right face

    v = new();
    v.Position = new(.5f, -.5f, -.5f);
    v.Color = new(.8f, .8f, .1f);
    m.Add(v); // 7

    v = new();
    v.Position = new(.5f, .5f, .5f);
    v.Color = new(.8f, .8f, .1f);
    m.Add(v); // 8

    v = new();
    v.Position = new(.5f, -.5f, .5f);
    v.Color = new(.8f, .8f, .1f);
    m.Add(v); // 9

    v = new();
    v.Position = new(.5f, -.5f, -.5f);
    v.Color = new(.8f, .8f, .1f);
    m.Add(v); // 10

    v = new();
    v.Position = new(.5f, .5f, -.5f);
    v.Color = new(.8f, .8f, .1f);
    m.Add(v); // 11

    v = new();
    v.Position = new(.5f, .5f, .5f);
    v.Color = new(.8f, .8f, .1f);
    m.Add(v); // 12

    // top face

    v = new();
    v.Position = new(-.5f, -.5f, -.5f);
    v.Color = new(.9f, .6f, .1f);
    m.Add(v); // 13

    v = new();
    v.Position = new(.5f, -.5f, .5f);
    v.Color = new(.9f, .6f, .1f);
    m.Add(v); // 14

    v = new();
    v.Position = new(-.5f, -.5f, .5f);
    v.Color = new(.9f, .6f, .1f);
    m.Add(v); // 15

    v = new();
    v.Position = new(-.5f, -.5f, -.5f);
    v.Color = new(.9f, .6f, .1f);
    m.Add(v); // 16

    v = new();
    v.Position = new(.5f, -.5f, -.5f);
    v.Color = new(.9f, .6f, .1f);
    m.Add(v); // 17

    v = new();
    v.Position = new(.5f, -.5f, .5f);
    v.Color = new(.9f, .6f, .1f);
    m.Add(v); // 18

    // bottom face

    v = new();
    v.Position = new(-.5f, .5f, -.5f);
    v.Color = new(.8f, .1f, .1f);
    m.Add(v); // 19

    v = new();
    v.Position = new(.5f, .5f, .5f);
    v.Color = new(.8f, .1f, .1f);
    m.Add(v); // 20

    v = new();
    v.Position = new(-.5f, .5f, .5f);
    v.Color = new(.8f, .1f, .1f);
    m.Add(v); // 21

    v = new();
    v.Position = new(-.5f, .5f, -.5f);
    v.Color = new(.8f, .1f, .1f);
    m.Add(v); // 22

    v = new();
    v.Position = new(.5f, .5f, -.5f);
    v.Color = new(.8f, .1f, .1f);
    m.Add(v); // 23

    v = new();
    v.Position = new(.5f, .5f, .5f);
    v.Color = new(.8f, .1f, .1f);
    m.Add(v); // 24

    // nose face

    v = new();
    v.Position = new(-.5f, -.5f, 0.5f);
    v.Color = new(.1f, .1f, .8f);
    m.Add(v); // 25

    v = new();
    v.Position = new(.5f, .5f, 0.5f);
    v.Color = new(.1f, .1f, .8f);
    m.Add(v); // 26

    v = new();
    v.Position = new(-.5f, .5f, 0.5f);
    v.Color = new(.1f, .1f, .8f);
    m.Add(v); // 27

    v = new();
    v.Position = new(-.5f, -.5f, 0.5f);
    v.Color = new(.1f, .1f, .8f);
    m.Add(v); // 28

    v = new();
    v.Position = new(.5f, -.5f, 0.5f);
    v.Color = new(.1f, .1f, .8f);
    m.Add(v); // 29

    v = new();
    v.Position = new(.5f, .5f, 0.5f);
    v.Color = new(.1f, .1f, .8f);
    m.Add(v); // 30

    // tail face

    v = new();
    v.Position = new(-.5f, -.5f, -0.5f);
    v.Color = new(.1f, .8f, .1f);
    m.Add(v); // 31

    v = new();
    v.Position = new(.5f, .5f, -0.5f);
    v.Color = new(.1f, .8f, .1f);
    m.Add(v); // 32

    v = new();
    v.Position = new(-.5f, .5f, -0.5f);
    v.Color = new(.1f, .8f, .1f);
    m.Add(v); // 33

    v = new();
    v.Position = new(-.5f, -.5f, -0.5f);
    v.Color = new(.1f, .8f, .1f);
    m.Add(v); // 34

    v = new();
    v.Position = new(.5f, -.5f, -0.5f);
    v.Color = new(.1f, .8f, .1f);
    m.Add(v); // 35

    v = new();
    v.Position = new(.5f, .5f, -0.5f);
    v.Color = new(.1f, .8f, .1f);
    m.Add(v); // 36


    for (int i = 0; i < m.Count; i++) {
      var target = m[i];
      target.Position += offset;
    }

    return new Model(_device, m.ToArray());
  }

  private void LoadEntities() {
    Console.WriteLine(Directory.GetCurrentDirectory());

    var en = new Entity();
    //en.AddComponent(GetCube(new Vector3(0, 0, 0)));
    // en.AddComponent(new Model(_device, GenericLoader.LoadModel("./Models/dwarf_test_model.obj")));
    en.AddComponent(new GenericLoader().LoadModel(_device, "./Models/dwarf_test_model.obj"));
    en.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    en.AddComponent(new Transform(new Vector3(0.0f, 1.0f, -5f)));
    // en.GetComponent<Transform>().Translation = new(0f, -1.5f, 2f);
    en.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
    en.GetComponent<Transform>().Rotation = new(180f, 0f, 0);
    AddEntity(en);
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