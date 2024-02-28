using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Global;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Rendering;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering;
using Dwarf.Rendering.Lightning;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Dwarf.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

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
  private TextureManager _textureManager = null!;
  private SystemCollection _systems = null!;
  private DescriptorPool _globalPool = null!;
  private VkDescriptorSet[] _globalDescriptorSets = [];
  private DwarfBuffer[] _uboBuffers = [];

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

  private Skybox _skybox = null!;
  private ImGuiController _imguiController = null!;
  private GlobalUniformBufferObject _ubo = new();

  public RenderAPI CurrentAPI { get; private set; }

  public Application(
    string appName = "Dwarf Vulkan",
    SystemCreationFlags systemCreationFlags = SystemCreationFlags.Renderer3D,
    bool debugMode = true
  ) {
    CurrentAPI = RenderAPI.Vulkan;

    VulkanDevice.s_EnableValidationLayers = debugMode;

    Window = new Window(1200, 900, appName);
    Device = new VulkanDevice(Window);
    Renderer = new Renderer(Window, Device);
    _systems = new SystemCollection();

    Application.Instance = this;

    _textureManager = new(Device);
    _systemCreationFlags = systemCreationFlags;
  }

  public void SetupScene(Scene scene) {
    _currentScene = scene;
  }

  public unsafe void Run() {
    _uboBuffers = new DwarfBuffer[Renderer.MAX_FRAMES_IN_FLIGHT];
    for (int i = 0; i < _uboBuffers.Length; i++) {
      _uboBuffers[i] = new(
        Device,
        (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>(),
        1,
        BufferUsage.UniformBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
        Device.Properties.limits.minUniformBufferOffsetAlignment
      );
      _uboBuffers[i].Map((ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());
    }

    _globalSetLayout = new DescriptorSetLayout.Builder(Device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    _globalDescriptorSets = new VkDescriptorSet[Renderer.MAX_FRAMES_IN_FLIGHT];
    for (int i = 0; i < _globalDescriptorSets.Length; i++) {
      var bufferInfo = _uboBuffers[i].GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());
      var writer = new VulkanDescriptorWriter(_globalSetLayout, _globalPool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out _globalDescriptorSets[i]);
    }

    SetupSystems(_systemCreationFlags, Device, Renderer, _globalSetLayout, null!);
    var objs3D = Entity.DistinctInterface<IRender3DElement>(_entities).ToArray();
    _systems.Render3DSystem?.Setup(objs3D, ref _textureManager);
    _systems.Render2DSystem?.Setup(Entity.Distinct<Sprite>(_entities).ToArray(), ref _textureManager);
    _systems.RenderUISystem?.Setup(_systems.Canvas, ref _textureManager);
    _systems.PointLightSystem?.Setup();
    _systems.PhysicsSystem?.Init(objs3D);

    _skybox = new(Device, _textureManager, Renderer, _globalSetLayout.GetDescriptorSetLayout());
    _imguiController = new(Device, Renderer);
    _imguiController.Init((int)Window.Extent.Width, (int)Window.Extent.Height);
    // _imguiController.InitResources(_renderer.GetSwapchainRenderPass(), _device.GraphicsQueue, "imgui_vertex", "imgui_fragment");

    MasterAwake(Entity.GetScripts(_entities));
    _onLoad?.Invoke();
    MasterStart(Entity.GetScripts(_entities));

    _renderThread = new Thread(RenderLoop);
    // _calculationThread = new Thread(CalculationLoop);


    _renderThread?.Start();
    // _calculationThread?.Start();

    while (!Window.ShouldClose) {
      MouseState.GetInstance().ScrollDelta = 0.0f;
      if (Window.IsMinimalized) {
        glfwWaitEvents();
      } else {
        glfwPollEvents();
      }

      Time.Tick();

      // Render();
      PerformCalculations();

      _onUpdate?.Invoke();
      MasterUpdate(Entity.GetScripts(_entities.Where(x => x.CanBeDisposed == false).ToArray()));

      GC.Collect(2, GCCollectionMode.Optimized, false);

      // Logger.Info($"Render: {_renderShouldClose}");
    }

    Device._mutex.WaitOne();
    try {
      var result = vkDeviceWaitIdle(Device.LogicalDevice);
      if (result != VkResult.Success) {
        Logger.Error(result.ToString());
      }
    } finally {
      Device._mutex.ReleaseMutex();
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
  private unsafe void Render(ThreadInfo threadInfo) {
    Frames.TickStart();
    _systems.ValidateSystems(
        _entities.ToArray(),
        Device, Renderer,
        _globalSetLayout.GetDescriptorSetLayout(),
        CurrentPipelineConfig,
        ref _textureManager
      );

    float aspect = Renderer.AspectRatio;
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

    var commandBuffer = Renderer.BeginFrame();
    if (commandBuffer != VkCommandBuffer.Null) {
      int frameIndex = Renderer.GetFrameIndex();
      // FrameInfo frameInfo = new();

      /*
      GlobalUniformBufferObject ubo = new() {
        Projection = _camera.GetComponent<Camera>().GetProjectionMatrix(),
        View = _camera.GetComponent<Camera>().GetViewMatrix(),

        // LightPosition = _camera.GetComponent<Transform>().Position,
        LightPosition = new Vector3(0, -5, 0),
        LightColor = new Vector4(1f, 1f, 1f, 1f),
        AmientLightColor = new Vector4(1f, 1f, 1f, 1f),
        CameraPosition = _camera.GetComponent<Transform>().Position
      };
      */

      _ubo.Projection = _camera.GetComponent<Camera>().GetProjectionMatrix();
      _ubo.View = _camera.GetComponent<Camera>().GetViewMatrix();
      _ubo.LightPosition = DirectionalLight.LightPosition;
      _ubo.LightColor = DirectionalLight.LightColor;
      _ubo.AmientLightColor = DirectionalLight.AmbientColor;
      _ubo.CameraPosition = _camera.GetComponent<Transform>().Position;

      fixed (GlobalUniformBufferObject* uboPtr = &_ubo) {
        _uboBuffers[frameIndex].WriteToBuffer((IntPtr)(uboPtr), (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());
      }

      var currentFrame = new FrameInfo();
      currentFrame.Camera = _camera.GetComponent<Camera>();
      currentFrame.CommandBuffer = commandBuffer;
      currentFrame.FrameIndex = frameIndex;
      currentFrame.GlobalDescriptorSet = _globalDescriptorSets[frameIndex];
      currentFrame.TextureManager = _textureManager;

      // render
      Renderer.BeginSwapchainRenderPass(commandBuffer);

      _onRender?.Invoke();
      _skybox.Render(currentFrame);
      _systems.UpdateSystems(_entities.ToArray(), currentFrame);

      _imguiController.Update(Time.DeltaTime);
      _onGUI?.Invoke();
      _imguiController.Render(currentFrame);

      Renderer.EndSwapchainRenderPass(commandBuffer);
      Renderer.EndFrame();

      Collect();
    }

    Frames.TickEnd();
  }

  private void PerformCalculations() {
    _systems.UpdateCalculationSystems(GetEntities().ToArray());
  }

  private unsafe void RenderLoop() {
    var pool = Device.CreateCommandPool();
    var threadInfo = new ThreadInfo() {
      CommandPool = pool,
      CommandBuffer = [Renderer.MAX_FRAMES_IN_FLIGHT]
    };

    VkCommandBufferAllocateInfo secondaryCmdBufAllocateInfo = new();
    secondaryCmdBufAllocateInfo.level = VkCommandBufferLevel.Primary;
    secondaryCmdBufAllocateInfo.commandPool = threadInfo.CommandPool;
    secondaryCmdBufAllocateInfo.commandBufferCount = 1;

    fixed (VkCommandBuffer* cmdBfPtr = threadInfo.CommandBuffer) {
      vkAllocateCommandBuffers(Device.LogicalDevice, &secondaryCmdBufAllocateInfo, cmdBfPtr).CheckResult();
    }

    Renderer.CreateCommandBuffers(threadInfo.CommandPool, VkCommandBufferLevel.Primary);

    while (!_renderShouldClose) {
      if (Window.IsMinimalized) continue;

      Render(threadInfo);

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    fixed (VkCommandBuffer* cmdBfPtrEnd = threadInfo.CommandBuffer) {
      vkFreeCommandBuffers(Device.LogicalDevice, threadInfo.CommandPool, (uint)Renderer.MAX_FRAMES_IN_FLIGHT, cmdBfPtrEnd);
    }

    Device.WaitQueue();
    Device.WaitDevice();

    vkDestroyCommandPool(Device.LogicalDevice, threadInfo.CommandPool, null);
  }

  private unsafe void CalculationLoop() {
    while (!_calculationShouldClose) {
      PerformCalculations();
    }
  }

  private void SetupSystems(
    SystemCreationFlags creationFlags,
    VulkanDevice device,
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
      Device.WaitDevice();
      Device.WaitQueue();
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

  public Entity? GetEntity(Guid entitiyId) {
    lock (_entitiesLock) {
      return _entities.Where(x => x.EntityID == entitiyId).First();
    }
  }

  public void RemoveEntityAt(int index) {
    lock (_entitiesLock) {
      Device.WaitDevice();
      Device.WaitQueue();
      _entities.RemoveAt(index);
    }
  }

  public void RemoveEntity(Entity entity) {
    lock (_entitiesLock) {
      Device.WaitDevice();
      Device.WaitQueue();
      _entities.Remove(entity);
    }
  }

  public void RemoveEntity(Guid id) {
    lock (_entitiesLock) {
      var target = _entities.Where((x) => x.EntityID == id).First();
      Device.WaitDevice();
      Device.WaitQueue();
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
    _globalPool = new DescriptorPool.Builder(Device)
      .SetMaxSets((uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .Build();

    await LoadTextures();
    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.LoadedTextures.Count}");
  }

  public async Task<Task> MultiThreadedTextureLoad(List<List<string>> paths) {
    var startTime = DateTime.UtcNow;
    List<Task> tasks = new();

    List<List<ITexture>> textures = [];
    for (int i = 0; i < paths.Count; i++) {
      var t = await TextureManager.AddTextures(Device, [.. paths[i]]);
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
    _skybox?.Dispose();
    _imguiController?.Dispose();

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
    Renderer?.Dispose();
    Window?.Dispose();
    Device?.Dispose();
  }

  private void Collect() {
    for (short i = 0; i < _entities.Count; i++) {
      if (_entities[i].CanBeDisposed) {
        Device.WaitDevice();
        Device.WaitQueue();

        _entities[i].DisposeEverything();
        RemoveEntity(_entities[i].EntityID);

        Device.WaitDevice();
        Device.WaitQueue();

      }
    }
  }

  public VulkanDevice Device { get; } = null!;
  public Window Window { get; } = null!;
  public TextureManager TextureManager => _textureManager;
  public Renderer Renderer { get; } = null!;
  public FrameInfo FrameInfo => _currentFrameInfo;
  public DirectionalLight DirectionalLight { get; } = new();
}