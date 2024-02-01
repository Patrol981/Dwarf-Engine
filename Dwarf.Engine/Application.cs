using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Rendering;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using System.Numerics;

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

using static Dwarf.GLFW.GLFW;
using Dwarf.Engine.Global;

namespace Dwarf.Engine;

public class Application {
  public static Application Instance { get; private set; } = null!;

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

  public PipelineConfigInfo CurrentPipelineConfig = new PipelineConfigInfo();

  private EventCallback? _onUpdate;
  private EventCallback? _onRender;
  private EventCallback? _onGUI;
  private EventCallback? _onLoad;

  private Window _window = null!;
  private Device _device = null!;
  private Renderer _renderer = null!;
  private TextureManager _textureManager = null!;
  private SystemCollection _systems = null!;
  private DescriptorPool _globalPool = null!;
  private VkDescriptorSet[] _globalDescriptorSets = [];
  private Vulkan.Buffer[] _uboBuffers = [];

  private List<Entity> _entities = new();
  private readonly object _entitiesLock = new object();

  private Entity _camera = new();

  private Scene _currentScene = null!;

  // ubos
  private DescriptorSetLayout _globalSetLayout = null!;
  private readonly SystemCreationFlags _systemCreationFlags;

  private FrameInfo _currentFrameInfo = new();

  private Thread _renderThread;
  private Thread _calculationThread;
  private bool _calculationShouldClose = false;
  private bool _renderShouldClose = false;

  public Application(
    string appName = "Dwarf Vulkan",
    SystemCreationFlags systemCreationFlags = SystemCreationFlags.Renderer3D,
    bool debugMode = true
  ) {
    Device.s_EnableValidationLayers = debugMode;

    _window = new Window(1200, 900, appName);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);
    _systems = new SystemCollection();

    Application.Instance = this;

    _textureManager = new(_device);

    _systemCreationFlags = systemCreationFlags;
  }

  public void SetupScene(Scene scene) {
    _currentScene = scene;
  }

  public unsafe void Run() {
    _uboBuffers = new Vulkan.Buffer[_renderer.MAX_FRAMES_IN_FLIGHT];
    for (int i = 0; i < _uboBuffers.Length; i++) {
      _uboBuffers[i] = new(
        _device,
        (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>(),
        1,
        VkBufferUsageFlags.UniformBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );
      _uboBuffers[i].Map((ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());
    }

    _globalSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    _globalDescriptorSets = new VkDescriptorSet[_renderer.MAX_FRAMES_IN_FLIGHT];
    for (int i = 0; i < _globalDescriptorSets.Length; i++) {
      var bufferInfo = _uboBuffers[i].GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());
      var writer = new DescriptorWriter(_globalSetLayout, _globalPool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out _globalDescriptorSets[i]);
    }

    SetupSystems(_systemCreationFlags, _device, _renderer, _globalSetLayout, null!);
    var objs3D = Entity.DistinctInterface<IRender3DElement>(_entities).ToArray();
    _systems.Render3DSystem?.Setup(objs3D, ref _textureManager);
    _systems.Render2DSystem?.Setup(Entity.Distinct<Sprite>(_entities).ToArray(), ref _textureManager);
    _systems.RenderUISystem?.Setup(_systems.Canvas, ref _textureManager);
    _systems.PhysicsSystem?.Init(objs3D);

    MasterAwake(Entity.GetScripts(_entities));
    _onLoad?.Invoke();
    MasterStart(Entity.GetScripts(_entities));

    _renderThread = new Thread(RenderLoop);
    // _calculationThread = new Thread(CalculationLoop);


    _renderThread?.Start();
    // _calculationThread?.Start();

    while (!_window.ShouldClose) {
      MouseState.GetInstance().ScrollDelta = 0.0f;
      glfwPollEvents();
      Time.Tick();

      // Render();
      PerformCalculations();

      _camera.GetComponent<Camera>().UpdateControls();
      _onUpdate?.Invoke();
      MasterUpdate(Entity.GetScripts(_entities.Where(x => x.CanBeDisposed == false).ToArray()));
      _onGUI?.Invoke();

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    var result = vkDeviceWaitIdle(_device.LogicalDevice);
    if (result != VkResult.Success) {
      Logger.Error(result.ToString());
    }

    _calculationShouldClose = true;
    _renderShouldClose = true;

    if (_renderThread != null && _renderThread.IsAlive)
      _renderThread?.Join();
    if (_calculationThread != null && _calculationThread.IsAlive)
      _calculationThread?.Join();

    for (int i = 0; i < _uboBuffers.Length; i++) {
      _uboBuffers[i].Dispose();
    }
    Cleanup();
  }

  public void SetCamera(Entity camera) {
    _camera = camera;
  }

  private static void MasterAwake(ReadOnlySpan<DwarfScript> entities) {
    for (short i = 0; i < entities.Length; i++) {
      entities[i].Awake();
    }
  }

  private static void MasterStart(ReadOnlySpan<DwarfScript> entities) {
    for (short i = 0; i < entities.Length; i++) {
      entities[i].Start();
    }
  }

  private static void MasterUpdate(ReadOnlySpan<DwarfScript> entities) {
    for (short i = 0; i < entities.Length; i++) {
      entities[i].Update();
    }
  }

  private unsafe void Render() {
    Frames.TickStart();
    _systems.ValidateSystems(
        _entities.ToArray(),
        _device, _renderer,
        _globalSetLayout.GetDescriptorSetLayout(),
        CurrentPipelineConfig,
        ref _textureManager
      );

    float aspect = _renderer.AspectRatio;
    if (aspect != _camera.GetComponent<Camera>().Aspect) {
      _camera.GetComponent<Camera>().Aspect = aspect;
      switch (_camera.GetComponent<Camera>().CameraType) {
        case CameraType.Perspective:
          _camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
          break;
        case CameraType.Orthographic:
          _camera.GetComponent<Camera>()?.SetOrthograpicProjection();
          break;
        default:
          break;
      }
    }

    var commandBuffer = _renderer.BeginFrame();
    if (commandBuffer != VkCommandBuffer.Null) {
      int frameIndex = _renderer.GetFrameIndex();
      // FrameInfo frameInfo = new();

      GlobalUniformBufferObject ubo = new() {
        Projection = _camera.GetComponent<Camera>().GetProjectionMatrix(),
        View = _camera.GetComponent<Camera>().GetViewMatrix(),

        // LightPosition = _camera.GetComponent<Transform>().Position,
        LightPosition = new Vector3(0, -5, 0),
        LightColor = new Vector4(1f, 1f, 1f, 1f),
        AmientLightColor = new Vector4(1f, 1f, 1f, 1f),
        CameraPosition = _camera.GetComponent<Transform>().Position
      };

      _uboBuffers[frameIndex].WriteToBuffer((IntPtr)(&ubo), (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());

      _currentFrameInfo.Camera = _camera.GetComponent<Camera>();
      _currentFrameInfo.CommandBuffer = commandBuffer;
      _currentFrameInfo.FrameIndex = frameIndex;
      _currentFrameInfo.GlobalDescriptorSet = _globalDescriptorSets[frameIndex];
      _currentFrameInfo.TextureManager = _textureManager;

      // render
      _renderer.BeginSwapchainRenderPass(commandBuffer);
      _onRender?.Invoke();
      _systems.UpdateSystems(_entities.ToArray(), _currentFrameInfo);
      _renderer.EndSwapchainRenderPass(commandBuffer);
      _renderer.EndFrame();

      Collect();
    }

    Frames.TickEnd();
  }

  private void PerformCalculations() {
    _systems.UpdateCalculationSystems(GetEntities().ToArray());
  }

  private unsafe void RenderLoop() {
    while (!_renderShouldClose) {
      Render();
    }
  }

  private unsafe void CalculationLoop() {
    while (!_calculationShouldClose) {
      PerformCalculations();
    }
  }

  private void SetupSystems(
    SystemCreationFlags creationFlags,
    Device device,
    Renderer renderer,
    DescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo
  ) {
    SystemCreator.CreateSystems(ref _systems, creationFlags, device, renderer, globalSetLayout, configInfo);
  }

  public SystemCollection GetSystems() {
    return _systems;
  }


  public void AddEntity(Entity entity) {
    lock (_entitiesLock) {
      _entities.Add(entity);
      MasterAwake(Entity.GetScripts(new[] { entity }));
      MasterStart(Entity.GetScripts(new[] { entity }));
    }
  }

  public List<Entity> GetEntities() {
    lock (_entitiesLock) {
      return _entities;
    }
  }

  public void RemoveEntityAt(int index) {
    lock (_entitiesLock) {
      _entities.RemoveAt(index);
    }
  }

  public void RemoveEntity(Entity entity) {
    lock (_entitiesLock) {
      _entities.Remove(entity);
    }
  }

  public void RemoveEntity(Guid id) {
    lock (_entitiesLock) {
      var target = _entities.Where((x) => x.EntityID == id).First();
      _entities.Remove(target);
    }
  }

  public void DestroyEntity(Entity entity) {
    lock (_entitiesLock) {
      entity.CanBeDisposed = true;
    }
  }

  public void RemoveEntityRange(int index, int count) {
    lock (_entitiesLock) {
      _entities.RemoveRange(index, count);
    }
  }

  public async void Init() {
    _globalPool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)_renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)_renderer.MAX_FRAMES_IN_FLIGHT)
      .Build();

    await LoadTextures();
    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.LoadedTextures.Count}");
  }

  public async Task<Task> MultiThreadedTextureLoad(List<List<string>> paths) {
    var startTime = DateTime.UtcNow;
    List<Task> tasks = new();

    List<List<Texture>> textures = [];
    for (int i = 0; i < paths.Count; i++) {
      var t = await TextureManager.AddTextures(_device, [.. paths[i]]);
      textures.Add([.. t]);
    }

    for (int i = 0; i < paths.Count; i++) {
      _textureManager.AddRange(textures[i].ToArray());
    }

    var endTime = DateTime.Now;
    Logger.Info($"[TEXTURE] Load Time {endTime - startTime}");

    return Task.CompletedTask;
  }

  private async Task<Task> LoadTextures() {
    _currentScene.LoadTextures();
    await LoadTexturesAsSeparateThreads(_currentScene.GetTexturePaths());
    return Task.CompletedTask;
  }

  public async Task<Task> LoadTexturesAsSeparateThreads(List<List<string>> paths) {
    await MultiThreadedTextureLoad(paths);
    Logger.Info("Done Loading Textures");
    return Task.CompletedTask;
  }

  private Task LoadEntities() {
    var startTime = DateTime.UtcNow;
    _currentScene.LoadEntities();
    _entities.AddRange(_currentScene.GetEntities());

    var targetCnv = Entity.Distinct<Canvas>(_entities);
    if (targetCnv.Length > 0) {
      _systems.Canvas = targetCnv[0].GetComponent<Canvas>();
    }

    var endTime = DateTime.Now;
    Logger.Info($"[Entities] Load Time {endTime - startTime}");
    return Task.CompletedTask;
  }

  private void Cleanup() {
    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      entities[i].GetComponent<Sprite>()?.Dispose();

      var u = entities[i].GetDrawables<IDrawable>();
      foreach (var e in u) {
        var t = e as IDrawable;
        t?.Dispose();
      }
    }

    _textureManager?.Dispose();
    _globalSetLayout.Dispose();
    _globalPool.Dispose();
    _systems?.Dispose();
    _renderer?.Dispose();
    _window?.Dispose();
    _device?.Dispose();
  }

  private void Collect() {
    var didChange = false;

    vkDeviceWaitIdle(_device.LogicalDevice);
    vkQueueWaitIdle(_device.PresentQueue);
    for (short i = 0; i < _entities.Count; i++) {
      if (_entities[i].CanBeDisposed) {
        didChange = true;
        // _calculationShouldClose = true;
        _entities[i].DisposeEverything();
        Application.Instance.RemoveEntity(_entities[i].EntityID);
      }
    }

    /*
    if (didChange) {
      _calculationThread?.Join();
      _calculationThread = new Thread(CalculationLoop);
      _calculationThread.Start();
      _calculationShouldClose = false;
    }
    */
  }

  public Device Device => _device;
  public Window Window => _window;
  public TextureManager TextureManager => _textureManager;
  public Renderer Renderer => _renderer;
  public FrameInfo FrameInfo => _currentFrameInfo;
}