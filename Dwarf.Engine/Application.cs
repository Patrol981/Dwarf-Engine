using System.Numerics;
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

// using static Dwarf.GLFW.GLFW;
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

  private List<Entity> _entities = [];
  private readonly Queue<Entity> _entitiesQueue = new();
  private readonly object _entitiesLock = new object();

  private Entity _camera = new();

  // ubos
  private DescriptorPool _globalPool = null!;
  private Dictionary<string, DescriptorSetLayout> _descriptorSetLayouts = [];

  private readonly SystemCreationFlags _systemCreationFlags;

  private Thread? _renderThread;
  private bool _renderShouldClose = false;
  private bool _newSceneShouldLoad = false;
  private bool _appExitRequest = false;

  private Skybox _skybox = null!;
  // private GlobalUniformBufferObject _ubo;
  private readonly unsafe GlobalUniformBufferObject* _ubo =
    (GlobalUniformBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<GlobalUniformBufferObject>());

  private FrameInfo _currentFrame = new();

  public RenderAPI CurrentAPI { get; private set; }
  public bool VSync { get; init; } = false;
  public bool Fullscreen { get; init; } = false;
  public readonly object ApplicationLock = new object();

  public const int ThreadTimeoutTimeMS = 1000;
  private bool _useImGui = true;

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

    Window = new Window(1200, 900, appName, Fullscreen, debugMode);
    Device = new VulkanDevice(Window);
    Renderer = new Renderer(Window, Device);
    Systems = new SystemCollection();
    StorageCollection = new StorageCollection(Device);

    _textureManager = new(Device);
    _systemCreationFlags = systemCreationFlags;

    Mutex = new Mutex(false);

    _onAppLoading = () => {
      DirectRPG.BeginCanvas();
      DirectRPG.CanvasText("Loading...");
      DirectRPG.EndCanvas();
    };

    Time.Init();
  }

  public void SetCurrentScene(Scene scene) {
    CurrentScene = scene;
  }

  public void LoadScene(Scene scene) {
    SetCurrentScene(scene);
    _newSceneShouldLoad = true;
  }
  private async void SceneLoadReactor() {
    Device.WaitDevice();
    Device.WaitQueue();

    await Coroutines.CoroutineRunner.Instance.StopAllCoroutines();

    Guizmos.Clear();
    Guizmos.Free();
    foreach (var e in _entities) {
      e.CanBeDisposed = true;
    }

    Logger.Info($"Waiting for entities to dispose... [{_entities.Count()}]");
    if (_entities.Count() > 0) {
      return;
    }
    _entities.Clear();
    _entities = [];

    if (!_renderShouldClose) {
      _renderShouldClose = true;
      return;
    }

    Logger.Info("Waiting for render process to close...");
    while (_renderShouldClose) {
    }
    _renderThread?.Join();

    Logger.Info("Waiting for render thread to close...");
    while (_renderThread!.IsAlive) {
    }

    _renderThread = new Thread(LoaderLoop) {
      Name = "App Loading Frontend Thread"
    };
    _renderThread.Start();

    Mutex.WaitOne();
    Systems.Dispose();
    StorageCollection.Dispose();
    foreach (var layout in _descriptorSetLayouts) {
      layout.Value.Dispose();
    }
    _descriptorSetLayouts = [];
    _globalPool.Dispose();
    _globalPool = null!;
    _skybox?.Dispose();

    StorageCollection = new(Device);
    Mutex.ReleaseMutex();
    await Init();

    _renderShouldClose = true;
    Logger.Info("Waiting for loading render process to close...");
    while (_renderShouldClose) {

    }

    _renderThread.Join();
    _renderThread = new Thread(RenderLoop) {
      Name = "Render Thread"
    };
    _renderThread.Start();

    _newSceneShouldLoad = false;
  }

  private async Task<Task> SetupScene() {
    if (CurrentScene == null) return Task.CompletedTask;

    await LoadTextures();
    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.LoadedTextures.Count}");

    return Task.CompletedTask;
  }

  public async void Run() {
    Logger.Info("[APPLICATION] Application started");

    _onLoadPrimaryResources?.Invoke();

    if (_useImGui) {
      GuiController = new(Device, Renderer);
      await GuiController.Init((int)Window.Extent.Width, (int)Window.Extent.Height);
    }

    _renderThread = new Thread(LoaderLoop);
    _renderThread.Name = "App Loading Frontend Thread";
    _renderThread.Start();

    await Init();

    _renderShouldClose = true;
    Logger.Info("Waiting for render process to close...");
    while (_renderShouldClose) {
    }
    _renderThread?.Join();

    Logger.Info("Waiting for render thread to close...");
    while (_renderThread!.IsAlive) {
    }

    _renderThread = new Thread(RenderLoop) {
      Name = "Render Thread"
    };
    _renderThread.Start();

    Logger.Info("[APPLICATION] Application loaded. Starting render thread.");
    WindowState.FocusOnWindow();

    while (!Window.ShouldClose) {
      MouseState.GetInstance().ScrollDelta = 0.0f;
      Time.Tick();
      Window.PollEvents();
      if (!Window.IsMinimalized) {
        Window.Show();
      } else {
        Window.WaitEvents();
      }

      PerformCalculations();

      _onUpdate?.Invoke();
      var updatable = _entities.Where(x => x.CanBeDisposed == false).ToArray();
      MasterUpdate(updatable.GetScripts());

      if (_newSceneShouldLoad) {
        SceneLoadReactor();
      }

      if (_appExitRequest) {
        HandleExit();
      }

      // GC.Collect(2, GCCollectionMode.Optimized, false);
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

    Cleanup();
  }

  public void SetCamera(Entity camera) {
    _camera = camera;
  }
  #region RESOURCES
  private unsafe Task InitResources() {
    _globalPool = new DescriptorPool.Builder(Device)
      .SetMaxSets(10)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.StorageBuffer, (uint)Renderer.MAX_FRAMES_IN_FLIGHT * 45)
      .Build();

    _descriptorSetLayouts.TryAdd("Global", new DescriptorSetLayout.Builder(Device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build());

    _descriptorSetLayouts.TryAdd("PointLight", new DescriptorSetLayout.Builder(Device)
      .AddBinding(0, VkDescriptorType.StorageBuffer, VkShaderStageFlags.AllGraphics)
      .Build());

    _descriptorSetLayouts.TryAdd("ObjectData", new DescriptorSetLayout.Builder(Device)
      .AddBinding(0, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Vertex)
      // .AddBinding(1, VkDescriptorType.StorageBuffer, VkShaderStageFlags.AllGraphics)
      .Build());

    _descriptorSetLayouts.TryAdd("JointsBuffer", new DescriptorSetLayout.Builder(Device)
      .AddBinding(0, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Vertex)
      .Build());

    StorageCollection.CreateStorage(
      Device,
      VkDescriptorType.UniformBuffer,
      BufferUsage.UniformBuffer,
      Renderer.MAX_FRAMES_IN_FLIGHT,
      (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>(),
      1,
      _descriptorSetLayouts["Global"],
      _globalPool,
      "GlobalStorage",
      Device.Properties.limits.minUniformBufferOffsetAlignment
    );

    StorageCollection.CreateStorage(
      Device,
      VkDescriptorType.StorageBuffer,
      BufferUsage.StorageBuffer,
      Renderer.MAX_FRAMES_IN_FLIGHT,
      (ulong)Unsafe.SizeOf<PointLight>(),
      MAX_POINT_LIGHTS_COUNT,
      _descriptorSetLayouts["PointLight"],
      _globalPool,
      "PointStorage",
      Device.Properties.limits.minStorageBufferOffsetAlignment
    );

    _descriptorSetLayouts.TryAdd("Texture", new DescriptorSetLayout.Builder(Device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
      .Build());

    Mutex.WaitOne();
    // SetupSystems(_systemCreationFlags, Device, Renderer, _globalSetLayout, null!);
    Systems.Setup(this, _systemCreationFlags, Device, Renderer, _descriptorSetLayouts, null!, ref _textureManager);

    StorageCollection.CreateStorage(
      Device,
      VkDescriptorType.StorageBuffer,
      BufferUsage.StorageBuffer,
      Renderer.MAX_FRAMES_IN_FLIGHT,
      (ulong)Unsafe.SizeOf<ObjectData>(),
      (ulong)Systems.Render3DSystem.LastKnownElemCount,
      _descriptorSetLayouts["ObjectData"],
      null!,
      "ObjectStorage",
      Device.Properties.limits.minStorageBufferOffsetAlignment,
      true
   );

    StorageCollection.CreateStorage(
      Device,
      VkDescriptorType.StorageBuffer,
      BufferUsage.StorageBuffer,
      Renderer.MAX_FRAMES_IN_FLIGHT,
      (ulong)Unsafe.SizeOf<Matrix4x4>(),
      Systems.Render3DSystem.LastKnownSkinnedElemJointsCount,
      _descriptorSetLayouts["JointsBuffer"],
      null!,
      "JointsStorage",
      Device.Properties.limits.minStorageBufferOffsetAlignment,
      true
    );

    _skybox = new(Device, _textureManager, Renderer, _descriptorSetLayouts["Global"].GetDescriptorSetLayout());
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
    if (CurrentScene == null) return Task.CompletedTask;
    CurrentScene.LoadTextures();
    var paths = CurrentScene.GetTexturePaths();

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
    if (CurrentScene == null) return Task.CompletedTask;
    var startTime = DateTime.UtcNow;

    Mutex.WaitOne();
    CurrentScene.LoadEntities();
    _entities.AddRange(CurrentScene.GetEntities());
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
    var ents = entities.ToArray();
    Parallel.ForEach(ents, (entity) => {
      entity.Awake();
    });
#endif
  }

  private static void MasterStart(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    var ents = entities.ToArray();
    Parallel.ForEach(ents, (entity) => {
      entity.Start();
    });
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
      MasterAwake(new[] { entity }.GetScripts());
      MasterStart(new[] { entity }.GetScripts());
      vkWaitForFences(Device.LogicalDevice, fence, true, VulkanDevice.FenceTimeout);
      unsafe {
        vkDestroyFence(Device.LogicalDevice, fence);
      }
      _entitiesQueue.Enqueue(entity);
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
    // Time.Tick();
    if (Window.IsMinimalized) return;

    Systems.ValidateSystems(
        _entities.ToArray(),
        Device, Renderer,
        _descriptorSetLayouts,
        CurrentPipelineConfig,
        ref _textureManager
      );

    float aspect = Renderer.AspectRatio;
    if (aspect != _camera.GetComponent<Camera>().Aspect) {
      _camera.GetComponent<Camera>().Aspect = aspect;
      switch (_camera.GetComponent<Camera>().CameraType) {
        case CameraType.Perspective:
          _camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.1f, 100f);
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

      _ubo->DirectionalLight = DirectionalLight;

      ReadOnlySpan<Entity> entities = _entities.ToArray();

      if (Systems.PointLightSystem != null) {
        Systems.PointLightSystem.Update(entities, out var pointLights);
        if (pointLights.Length > 1) {
          _ubo->PointLightsLength = pointLights.Length;
          fixed (PointLight* pPointLights = pointLights) {
            StorageCollection.WriteBuffer(
              "PointStorage",
              frameIndex,
              (nint)pPointLights,
              (ulong)Unsafe.SizeOf<PointLight>() * MAX_POINT_LIGHTS_COUNT
            );
          }
        } else {
          _ubo->PointLightsLength = 0;
        }
      }

      Systems.Render3DSystem.Update(
        _entities.ToArray().DistinctI3D(),
        out var objectData,
        out var skinnedObjects,
        out var flatJoints
      );
      fixed (ObjectData* pObjectData = objectData) {
        StorageCollection.WriteBuffer(
          "ObjectStorage",
          frameIndex,
          (nint)pObjectData,
          (ulong)Unsafe.SizeOf<ObjectData>() * (ulong)objectData.Length
        );
      }

      ReadOnlySpan<Matrix4x4> flatArray = [.. flatJoints];
      fixed (Matrix4x4* pMatrices = flatArray) {
        StorageCollection.WriteBuffer(
          "JointsStorage",
          frameIndex,
          (nint)pMatrices,
          (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)flatArray.Length
        );
      }

      StorageCollection.WriteBuffer(
        "GlobalStorage",
        frameIndex,
        (nint)_ubo,
        (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>()
      );

      _currentFrame.Camera = _camera.GetComponent<Camera>();
      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;
      _currentFrame.GlobalDescriptorSet = StorageCollection.GetDescriptor("GlobalStorage", frameIndex);
      _currentFrame.PointLightsDescriptorSet = StorageCollection.GetDescriptor("PointStorage", frameIndex);
      _currentFrame.ObjectDataDescriptorSet = StorageCollection.GetDescriptor("ObjectStorage", frameIndex);
      _currentFrame.JointsBufferDescriptorSet = StorageCollection.GetDescriptor("JointsStorage", frameIndex);
      _currentFrame.TextureManager = _textureManager;

      // render
      Renderer.BeginSwapchainRenderPass(commandBuffer);

      _onRender?.Invoke();
      _skybox.Render(_currentFrame);
      Systems.UpdateSystems(_entities.ToArray(), _currentFrame);

      if (_useImGui) {
        GuiController.Update(Time.StopwatchDelta);
        _onGUI?.Invoke();
      }
      var updatable = _entities.Where(x => x.CanBeDisposed == false).ToArray();
      MasterRenderUpdate(updatable.GetScripts());

      if (_useImGui) {
        GuiController.Render(_currentFrame);
      }

      Mutex.WaitOne();
      Renderer.EndSwapchainRenderPass(commandBuffer);
      Renderer.EndFrame();
      Mutex.ReleaseMutex();

      StorageCollection.CheckSize("ObjectStorage", frameIndex, Systems.Render3DSystem.LastKnownElemCount, _descriptorSetLayouts["ObjectData"]);
      StorageCollection.CheckSize("JointsStorage", frameIndex, (int)Systems.Render3DSystem.LastKnownSkinnedElemJointsCount, _descriptorSetLayouts["JointsBuffer"]);

      Collect();
    }

    if (_entitiesQueue.Count > 0) {
      Mutex.WaitOne();
      while (_entitiesQueue.Count > 0) {
        _entities.Add(_entitiesQueue.Dequeue());
      }
      Mutex.ReleaseMutex();
    }
  }

  internal unsafe void RenderLoader() {
    var commandBuffer = Renderer.BeginFrame();
    if (commandBuffer != VkCommandBuffer.Null) {
      int frameIndex = Renderer.GetFrameIndex();

      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;

      Renderer.BeginSwapchainRenderPass(commandBuffer);

      if (_useImGui) {
        GuiController.Update(Time.StopwatchDelta);
        _onAppLoading?.Invoke();
        GuiController.Render(_currentFrame);
      }

      Mutex.WaitOne();
      Renderer.EndSwapchainRenderPass(commandBuffer);
      Renderer.EndFrame();
      Mutex.ReleaseMutex();
    }
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

    VkCommandBufferAllocateInfo secondaryCmdBufAllocateInfo = new() {
      level = VkCommandBufferLevel.Primary,
      commandPool = threadInfo.CommandPool,
      commandBufferCount = 1
    };

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
    Mutex.WaitOne();
    var pool = Device.CreateCommandPool();
    var threadInfo = new ThreadInfo() {
      CommandPool = pool,
      CommandBuffer = [Renderer.MAX_FRAMES_IN_FLIGHT]
    };

    Renderer.CreateCommandBuffers(threadInfo.CommandPool, VkCommandBufferLevel.Primary);
    Mutex.ReleaseMutex();

    while (!_renderShouldClose) {
      if (Window.IsMinimalized) continue;

      Render(threadInfo);

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }
    Logger.Info("Closing Renderer");

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
    foreach (var layout in _descriptorSetLayouts) {
      layout.Value.Dispose();
    }
    _globalPool.Dispose();
    unsafe {
      MemoryUtils.FreeIntPtr<GlobalUniformBufferObject>((nint)_ubo);
    }
    StorageCollection?.Dispose();
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

  public void CloseApp() {
    _appExitRequest = true;
  }

  private async void HandleExit() {
    await Coroutines.CoroutineRunner.Instance.StopAllCoroutines();

    Guizmos.Clear();
    Guizmos.Free();
    foreach (var e in _entities) {
      e.CanBeDisposed = true;
    }

    Logger.Info($"Waiting for entities to dispose... [{_entities.Count()}]");
    if (_entities.Count() > 0) {
      return;
    }

    if (!_renderShouldClose) {
      _renderShouldClose = true;
      return;
    }

    Logger.Info("Waiting for render process to close...");
    while (_renderShouldClose) {
    }
    _renderThread?.Join();

    Logger.Info("Waiting for render thread to close...");
    while (_renderThread!.IsAlive) {
    }

    Systems.PhysicsSystem?.Dispose();

    System.Environment.Exit(1);
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
  public StorageCollection StorageCollection { get; private set; } = null!;
  public Scene CurrentScene { get; private set; } = null!;
  public bool UseImGui => _useImGui;

  public const int MAX_POINT_LIGHTS_COUNT = 128;
}