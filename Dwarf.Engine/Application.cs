using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Rendering;
using Dwarf.Rendering.Lightning;
using Dwarf.Rendering.UI;
using Dwarf.Rendering.UI.DirectRPG;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Dwarf.Windowing;

using Vortice.Vulkan;

using static Dwarf.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

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

  public void SetAppLoaderCallback(EventCallback eventCallback) {
    _onAppLoading = eventCallback;
  }

  public void SetOnLoadPrimaryCallback(EventCallback eventCallback) {
    _onLoadPrimaryResources = eventCallback;
  }

  public void SetOnLoadCallback(EventCallback eventCallback) {
    _onLoad = eventCallback;
  }

  public PipelineConfigInfo CurrentPipelineConfig = new PipelineConfigInfo();

  private EventCallback? _onUpdate;
  private EventCallback? _onRender;
  private EventCallback? _onGUI;
  private EventCallback? _onAppLoading;
  private EventCallback? _onLoad;
  private EventCallback? _onLoadPrimaryResources;
  private TextureManager _textureManager = null!;

  private readonly List<Entity> _entities = new();
  private readonly object _entitiesLock = new object();

  private Entity _camera = new();

  private Scene _currentScene = null!;

  // ubos
  private DescriptorPool _globalPool = null!;
  private VkDescriptorSet[] _globalDescriptorSets = [];
  private DwarfBuffer[] _uboBuffers = [];

  private DescriptorSetLayout _globalSetLayout = null!;
  private DescriptorSetLayout _globalTextureSetLayout = null!;

  private readonly SystemCreationFlags _systemCreationFlags;

  private Thread? _renderThread;
  private bool _renderShouldClose = false;

  private Skybox _skybox = null!;
  // private GlobalUniformBufferObject _ubo;
  private readonly unsafe GlobalUniformBufferObject* _ubo =
    (GlobalUniformBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<GlobalUniformBufferObject>());

  private FrameInfo _currentFrame = new();

  public RenderAPI CurrentAPI { get; private set; }
  public bool VSync { get; init; } = false;
  public bool Fullscreen { get; init; } = false;
  public readonly object ApplicationLock = new object();

  public Application(
    string appName = "Dwarf Vulkan",
    SystemCreationFlags systemCreationFlags = SystemCreationFlags.Renderer3D,
    bool vsync = false,
    bool fullscreen = false,
    bool debugMode = true
  ) {
    Application.Instance = this;
    CurrentAPI = RenderAPI.Vulkan;
    VSync = vsync;
    Fullscreen = fullscreen;

    VulkanDevice.s_EnableValidationLayers = debugMode;

    Window = new Window(1200, 900, appName, Fullscreen);
    Device = new VulkanDevice(Window);
    Renderer = new Renderer(Window, Device);
    Systems = new SystemCollection();

    _textureManager = new(Device);
    _systemCreationFlags = systemCreationFlags;

    Mutex = new Mutex(false);

    _onAppLoading = () => {
      DirectRPG.BeginCanvas();
      DirectRPG.CanvasText("Loading...");
      DirectRPG.EndCanvas();
    };
  }

  public void SetCurrentScene(Scene scene) {
    _currentScene = scene;
  }

  private async Task<Task> SetupScene() {
    if (_currentScene == null) return Task.CompletedTask;

    await LoadTextures();
    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.LoadedTextures.Count}");

    return Task.CompletedTask;
  }

  public async void Run() {
    Logger.Info("[APPLICATION] Application started");

    WindowState.SetCursorMode(GLFW.InputValue.GLFW_CURSOR_NORMAL);

    _onLoadPrimaryResources?.Invoke();

    GuiController = new(Device, Renderer);
    await GuiController.Init((int)Window.Extent.Width, (int)Window.Extent.Height);

    _renderThread = new Thread(LoaderLoop);
    _renderThread.Name = "App Loading Frontend Thread";
    _renderThread.Start();

    await Init();

    _renderShouldClose = true;
    Logger.Info("Waiting for render process to close...");
    while (_renderShouldClose) {

    }
    _renderThread.Join();
    _renderThread = new Thread(RenderLoop);
    _renderThread.Name = "Render Thread";
    _renderThread.Start();
    // _calculationThread = new Thread(CalculationLoop);

    Logger.Info("[APPLICATION] Application loaded. Starting render thread.");
    WindowState.FocusOnWindow();

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
      var updatable = _entities.Where(x => x.CanBeDisposed == false).ToArray();
      MasterUpdate(updatable.GetScripts());

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    Mutex.WaitOne();
    try {
      var result = vkDeviceWaitIdle(Device.LogicalDevice);
      if (result != VkResult.Success) {
        Logger.Error(result.ToString());
      }
    } finally {
      Mutex.ReleaseMutex();
    }


    _renderShouldClose = true;

    if (_renderThread != null && _renderThread.IsAlive)
      _renderThread?.Join();

    for (int i = 0; i < _uboBuffers.Length; i++) {
      _uboBuffers[i].Dispose();
    }
    Cleanup();
  }

  public void SetCamera(Entity camera) {
    _camera = camera;
  }
  #region RESOURCES
  private unsafe Task InitResources() {
    _globalPool = new DescriptorPool.Builder(Device)
      .SetMaxSets((uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .Build();

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
      _ = new VulkanDescriptorWriter(_globalSetLayout, _globalPool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out _globalDescriptorSets[i]);
    }

    _globalTextureSetLayout = new DescriptorSetLayout.Builder(Device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
      .Build();

    Mutex.WaitOne();
    // SetupSystems(_systemCreationFlags, Device, Renderer, _globalSetLayout, null!);
    Systems.Setup(this, _systemCreationFlags, Device, Renderer, _globalSetLayout, null!, ref _textureManager);

    _skybox = new(Device, _textureManager, Renderer, _globalSetLayout.GetDescriptorSetLayout());
    Mutex.ReleaseMutex();
    // _imguiController.InitResources(_renderer.GetSwapchainRenderPass(), _device.GraphicsQueue, "imgui_vertex", "imgui_fragment");

    MasterAwake(_entities.GetScripts());
    _onLoad?.Invoke();
    MasterStart(_entities.GetScripts());

    return Task.CompletedTask;
  }

  private async Task<Task> Init() {
    var tasks = new Task[] {
      await SetupScene(),
      InitResources(),
    };

    // await SetupScene();
    // InitResources();
    await Task.WhenAll(tasks);

    return Task.CompletedTask;
  }

  private async Task<Task> LoadTextures() {
    if (_currentScene == null) return Task.CompletedTask;
    _currentScene.LoadTextures();
    var paths = _currentScene.GetTexturePaths();

    var startTime = DateTime.UtcNow;
    List<Task> tasks = [];

    List<List<ITexture>> textures = [];
    for (int i = 0; i < paths.Count; i++) {
      var t = await TextureManager.AddTextures(Device, [.. paths[i]]);
      textures.Add([.. t]);
    }

    for (int i = 0; i < paths.Count; i++) {
      _textureManager.AddRange([.. textures[i]]);
    }

    var endTime = DateTime.Now;
    Logger.Info($"[TEXTURE] Load Time {endTime - startTime}");


    return Task.CompletedTask;
  }

  private Task LoadEntities() {
    if (_currentScene == null) return Task.CompletedTask;
    var startTime = DateTime.UtcNow;

    Mutex.WaitOne();
    _currentScene.LoadEntities();
    _entities.AddRange(_currentScene.GetEntities());
    Mutex.ReleaseMutex();

    var targetCnv = _entities.Distinct<Canvas>();
    if (targetCnv.Length > 0) {
      Systems.Canvas = targetCnv[0].GetComponent<Canvas>();
    }

    var endTime = DateTime.Now;
    Logger.Info($"[Entities] Load Time {endTime - startTime}");
    return Task.CompletedTask;
  }

  #endregion RESOURCES
  #region ENTITY_FLOW
  private static void MasterAwake(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].Awake();
    }
#endif
  }

  private static void MasterStart(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].Start();
    }
#endif
  }

  private static void MasterUpdate(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].Update();
    }
#endif
  }

  private static void MasterRenderUpdate(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].RenderUpdate();
    }
#endif
  }

  public void AddEntity(Entity entity) {
    lock (_entitiesLock) {
      var fence = Device.CreateFence(VkFenceCreateFlags.Signaled);
      _entities.Add(entity);
      MasterAwake(new[] { entity }.GetScripts());
      MasterStart(new[] { entity }.GetScripts());
      vkWaitForFences(Device.LogicalDevice, fence, true, VulkanDevice.FenceTimeout);
      unsafe {
        vkDestroyFence(Device.LogicalDevice, fence);
      }
    }
  }

  public void AddEntities(Entity[] entities) {
    foreach (var entity in entities) {
      AddEntity(entity);
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
  #endregion ENTITY_FLOW
  #region APPLICATION_LOOP
  private unsafe void Render(ThreadInfo threadInfo) {
    Frames.TickStart();
    Systems.ValidateSystems(
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

      _ubo->Projection = _camera.GetComponent<Camera>().GetProjectionMatrix();
      _ubo->View = _camera.GetComponent<Camera>().GetViewMatrix();
      _ubo->CameraPosition = _camera.GetComponent<Transform>().Position;
      _ubo->Layer = 1;

      // _ubo.LightPosition = DirectionalLight.LightPosition;
      // _ubo.LightColor = DirectionalLight.LightColor;
      // _ubo.AmbientColor = DirectionalLight.AmbientColor;

      _ubo->DirectionalLight = DirectionalLight;
      /*
      _ubo.LightPosition = DirectionalLight.LightPosition;
      _ubo.LightColor = DirectionalLight.LightColor;
      _ubo.AmientLightColor = DirectionalLight.AmbientColor;
      _ubo.CameraPosition = _camera.GetComponent<Transform>().Position;
      */


      _uboBuffers[frameIndex].WriteToBuffer((nint)(_ubo), (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());

      // var currentFrame = new FrameInfo();
      _currentFrame.Camera = _camera.GetComponent<Camera>();
      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;
      _currentFrame.GlobalDescriptorSet = _globalDescriptorSets[frameIndex];
      _currentFrame.TextureManager = _textureManager;

      Systems.PointLightSystem?.Update(ref _currentFrame, ref *_ubo, _entities.ToArray());
      Logger.Info($"{_ubo->PointLights[0].LightPosition}");

      // render
      Renderer.BeginSwapchainRenderPass(commandBuffer);

      _onRender?.Invoke();
      _skybox.Render(_currentFrame);
      Systems.UpdateSystems(_entities.ToArray(), _currentFrame);

      GuiController.Update(Time.DeltaTime);
      _onGUI?.Invoke();
      var updatable = _entities.Where(x => x.CanBeDisposed == false).ToArray();
      MasterRenderUpdate(updatable.GetScripts());
      GuiController.Render(_currentFrame);

      Renderer.EndSwapchainRenderPass(commandBuffer);
      Renderer.EndFrame();

      Collect();
    }

    Frames.TickEnd();
  }

  internal unsafe void RenderLoader() {
    Frames.TickStart();

    var commandBuffer = Renderer.BeginFrame();
    if (commandBuffer != VkCommandBuffer.Null) {
      int frameIndex = Renderer.GetFrameIndex();

      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;

      Renderer.BeginSwapchainRenderPass(commandBuffer);

      GuiController.Update(Time.DeltaTime);
      _onAppLoading?.Invoke();
      GuiController.Render(_currentFrame);

      Mutex.WaitOne();
      Renderer.EndSwapchainRenderPass(commandBuffer);
      Renderer.EndFrame();
      Mutex.ReleaseMutex();
    }


    Frames.TickEnd();
  }

  private void PerformCalculations() {
    Systems.UpdateCalculationSystems(GetEntities().ToArray());
  }

  internal unsafe void LoaderLoop() {
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
      RenderLoader();
    }

    fixed (VkCommandBuffer* cmdBfPtrEnd = threadInfo.CommandBuffer) {
      vkFreeCommandBuffers(Device.LogicalDevice, threadInfo.CommandPool, (uint)Renderer.MAX_FRAMES_IN_FLIGHT, cmdBfPtrEnd);
    }

    Device.WaitQueue();
    Device.WaitDevice();

    vkDestroyCommandPool(Device.LogicalDevice, threadInfo.CommandPool, null);

    _renderShouldClose = false;
  }

  internal unsafe void RenderLoop() {
    var pool = Device.CreateCommandPool();
    var threadInfo = new ThreadInfo() {
      CommandPool = pool,
      CommandBuffer = [Renderer.MAX_FRAMES_IN_FLIGHT]
    };

    Renderer.CreateCommandBuffers(threadInfo.CommandPool, VkCommandBufferLevel.Primary);

    while (!_renderShouldClose) {
      if (Window.IsMinimalized) continue;

      Render(threadInfo);

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    Device.WaitQueue();
    Device.WaitDevice();

    vkDestroyCommandPool(Device.LogicalDevice, threadInfo.CommandPool, null);

    _renderShouldClose = false;
  }
  #endregion APPLICATION_LOOP

  private void Cleanup() {
    _skybox?.Dispose();
    GuiController?.Dispose();

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
    _globalTextureSetLayout?.Dispose();
    _globalPool.Dispose();
    unsafe {
      MemoryUtils.FreeIntPtr<GlobalUniformBufferObject>((nint)_ubo);
    }
    Systems?.Dispose();
    Renderer?.Dispose();
    Window?.Dispose();
    Device?.Dispose();
  }

  private void Collect() {
    for (short i = 0; i < _entities.Count; i++) {
      if (_entities[i].CanBeDisposed) {
        Device.WaitDevice();

        _entities[i].DisposeEverything();
        RemoveEntity(_entities[i].EntityID);
      }
    }
  }

  public VulkanDevice Device { get; } = null!;
  public Mutex Mutex { get; private set; }
  public Window Window { get; } = null!;
  public TextureManager TextureManager => _textureManager;
  public Renderer Renderer { get; } = null!;
  public FrameInfo FrameInfo => _currentFrame;
  public DirectionalLight DirectionalLight { get; set; } = DirectionalLight.New();
  public ImGuiController GuiController { get; private set; } = null!;
  public SystemCollection Systems { get; } = null!;
}