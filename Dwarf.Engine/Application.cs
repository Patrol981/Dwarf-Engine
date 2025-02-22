using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Lightning;
using Dwarf.Rendering.UI;
using Dwarf.Rendering.UI.DirectRPG;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Dwarf.Windowing;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vma;
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
  private readonly Queue<MeshRenderer> _reloadQueue = new();
  private readonly object _entitiesLock = new object();

  private Entity _camera = new();

  // ubos
  private DescriptorPool _globalPool = null!;
  private Dictionary<string, DescriptorSetLayout> _descriptorSetLayouts = [];

  private readonly SystemCreationFlags _systemCreationFlags;
  private readonly SystemConfiguration _systemConfiguration;

  private Thread? _renderThread;
  private bool _renderShouldClose = false;
  private bool _newSceneShouldLoad = false;
  private bool _appExitRequest = false;

  private Skybox? _skybox = null;
  public bool UseSkybox = true;

  // private GlobalUniformBufferObject _ubo;
  private readonly unsafe GlobalUniformBufferObject* _ubo =
    (GlobalUniformBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<GlobalUniformBufferObject>());

  private FrameInfo _currentFrame = new();

  public RenderAPI CurrentAPI { get; private set; }
  public bool VSync { get; init; } = false;
  public bool Fullscreen { get; init; } = false;
  public readonly object ApplicationLock = new object();

  public const int ThreadTimeoutTimeMS = 1000;

  public Vector3 FogValue = Vector3.UnitX;
  public bool UseFog = true;

  public Application(
    string appName = "Dwarf Vulkan",
    Vector2I windowSize = default!,
    SystemCreationFlags systemCreationFlags = SystemCreationFlags.Renderer3D,
    SystemConfiguration? systemConfiguration = default,
    bool vsync = false,
    bool fullscreen = false,
    bool debugMode = true
  ) {
    Instance = this;
    CurrentAPI = RenderAPI.Vulkan;
    VSync = vsync;
    Fullscreen = fullscreen;

    VulkanDevice.s_EnableValidationLayers = debugMode;

    windowSize ??= new(1200, 900);

    Window = new Window(windowSize.X, windowSize.Y);
    Window.Init(appName, Fullscreen, debugMode);

    Device = new VulkanDevice(Window);

    VmaAllocatorCreateFlags allocatorFlags = VmaAllocatorCreateFlags.KHRDedicatedAllocation | VmaAllocatorCreateFlags.KHRBindMemory2;
    VmaAllocatorCreateInfo allocatorCreateInfo = new() {
      flags = allocatorFlags,
      instance = Device.VkInstance,
      vulkanApiVersion = VkVersion.Version_1_4,
      physicalDevice = Device.PhysicalDevice,
      device = Device.LogicalDevice,
    };
    vmaCreateAllocator(allocatorCreateInfo, out var allocator);
    VmaAllocator = allocator;

    // Renderer = new Renderer(Window, Device);
    Renderer = new DynamicRenderer(Window, Device);
    Systems = new SystemCollection();
    StorageCollection = new StorageCollection(VmaAllocator, Device);

    _textureManager = new(VmaAllocator, Device);
    _systemCreationFlags = systemCreationFlags;

    systemConfiguration ??= SystemConfiguration.Default;
    _systemConfiguration = systemConfiguration;

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
    Mutex.WaitOne();
    Device.WaitDevice();
    Device.WaitQueue();
    Mutex.ReleaseMutex();

    await Coroutines.CoroutineRunner.Instance.StopAllCoroutines();

    Guizmos.Clear();
    Guizmos.Free();
    foreach (var e in _entities) {
      e.CanBeDisposed = true;
    }

    // Mutex.WaitOne();
    // while (_entities.Count > 0) {
    //   Logger.Info($"Waiting for entities to dispose... [{_entities.Count}]");
    //   Collect();
    //   _entities.Clear();
    //   _entities = [];
    // }

    Logger.Info($"Waiting for entities to dispose... [{_entities.Count}]");
    if (_entities.Count > 0) {
      return;
    }

    // Mutex.ReleaseMutex();

    _renderShouldClose = true;
    // if (!_renderShouldClose) {

    //   return;
    // }

    // while (_renderShouldClose) {

    // }

    Logger.Info("Waiting for render process to close...");
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
    _textureManager.DisposeLocal();

    StorageCollection = new(VmaAllocator, Device);
    Mutex.ReleaseMutex();
    await Init();

    _renderShouldClose = true;
    Logger.Info("[Scene Reactor Finalizer] Waiting for loading render process to close...");
    // while (_renderShouldClose) {

    // }
    while (_renderThread.IsAlive) {

    }

    _newSceneShouldLoad = false;
    _renderThread.Join();
    _renderThread = new Thread(RenderLoop) {
      Name = "Render Thread"
    };
    _renderThread.Start();

  }

  private async Task<Task> SetupScene() {
    if (CurrentScene == null) return Task.CompletedTask;

    await LoadTextures();
    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.PerSceneLoadedTextures.Count}");

    return Task.CompletedTask;
  }

  public async void Run() {
    Logger.Info("[APPLICATION] Application started");

    _onLoadPrimaryResources?.Invoke();

    if (UseImGui) {
      GuiController = new(VmaAllocator, Device, Renderer);
      await GuiController.Init((int)Window.Extent.Width, (int)Window.Extent.Height);
    }

    _renderThread = new Thread(LoaderLoop) {
      Name = "App Loading Frontend Thread"
    };
    _renderThread.Start();

    await Init();

    _renderShouldClose = true;
    Logger.Info("Waiting for renderer to close...");
    int x = 0;
    while (_renderShouldClose) { Console.Write(""); }
    _renderThread?.Join();

    Logger.Info("Waiting for render thread to close...");
    while (_renderThread!.IsAlive) {
    }

    _renderShouldClose = false;
    Logger.Info("[APPLICATION] Application loaded. Starting render thread.");
    _renderThread = new Thread(RenderLoop) {
      Name = "Render Thread"
    };
    _renderThread.Start();

    Logger.Info("[APPLICATION] Application loaded. Starting render thread.");

    while (!Window.ShouldClose) {
      Input.ScrollDelta = 0.0f;
      Time.Tick();
      Window.PollEvents();
      if (!Window.IsMinimalized) {
        Window.Show();
      } else {
        Window.WaitEvents();
      }

      PerformCalculations();

      var updatable = _entities.Where(x => x.CanBeDisposed == false).ToArray();
      MasterFixedUpdate(updatable.GetScriptsAsSpan());
      _onUpdate?.Invoke();
      MasterUpdate(updatable.GetScriptsAsArray());

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
      .AddPoolSize(VkDescriptorType.InputAttachment, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.SampledImage, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.Sampler, (uint)Renderer.MAX_FRAMES_IN_FLIGHT)
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

    // _descriptorSetLayouts.TryAdd("InputAttachments", new DescriptorSetLayout.Builder(Device)
    //   .AddBinding(0, VkDescriptorType.InputAttachment, VkShaderStageFlags.Fragment)
    //   .Build());

    // StorageCollection.CreateStorage(
    //   Device,
    //   VkDescriptorType.InputAttachment,
    //   BufferUsage.UniformBuffer
    // )

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

    //_descriptorSetLayouts.TryAdd("Texture", new DescriptorSetLayout.Builder(Device)
    //  .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    //  .Build());

    Mutex.WaitOne();
    // SetupSystems(_systemCreationFlags, Device, Renderer, _globalSetLayout, null!);
    Systems.Setup(
      this,
      _systemCreationFlags,
      _systemConfiguration,
      VmaAllocator,
      Device,
      Renderer,
      _descriptorSetLayouts,
      null!,
      ref _textureManager
    );

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

    if (UseSkybox) {
      _skybox = new(
        VmaAllocator,
        Device,
        _textureManager,
        Renderer,
        _descriptorSetLayouts["Global"].GetDescriptorSetLayout()
      );
    }
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
      InitResources()
    };

    await Task.WhenAll(tasks);

    return Task.CompletedTask;
  }

  private async Task<Task> LoadTextures() {
    if (CurrentScene == null) return Task.CompletedTask;
    CurrentScene.LoadTextures();
    var paths = CurrentScene.GetTexturePaths();

    var startTime = DateTime.UtcNow;

    List<List<ITexture>> textures = [];
    for (int i = 0; i < paths.Count; i++) {
      var t = await TextureManager.AddTextures(VmaAllocator, Device, [.. paths[i]]);
      textures.Add([.. t]);
    }

    for (int i = 0; i < paths.Count; i++) {
      _textureManager.AddRangeLocal([.. textures[i]]);
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

  private static void MasterUpdate(DwarfScript[] entities) {
#if RUNTIME
    Parallel.For(0, entities.Length, i => { entities[i].Update(); });
    // for (short i = 0; i < entities.Length; i++) {
    //   entities[i].Update();
    // }
#endif
  }

  private static void MasterFixedUpdate(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].FixedUpdate();
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
      MasterAwake(new[] { entity }.GetScriptsAsSpan());
      MasterStart(new[] { entity }.GetScriptsAsSpan());
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
      if (_entities.Count == 0) return;
      var target = _entities.Where((x) => x.EntityID == id).FirstOrDefault();
      if (target == null) return;
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

  public void AddModelToReloadQueue(MeshRenderer meshRenderer) {
    _reloadQueue.Enqueue(meshRenderer);
  }
  #endregion ENTITY_FLOW
  #region APPLICATION_LOOP
  private unsafe void Render(ThreadInfo threadInfo) {
    // Time.Tick();
    // Logger.Info("TICK");

    Time.RenderTick();
    if (Window.IsMinimalized) return;

    Systems.ValidateSystems(
        _entities.ToArray(),
        VmaAllocator, Device, Renderer,
        _descriptorSetLayouts,
        CurrentPipelineConfig,
        ref _textureManager
      );

    float aspect = Renderer.AspectRatio;
    var cameraAsppect = _camera.TryGetComponent<Camera>()?.Aspect;
    if (aspect != cameraAsppect && cameraAsppect != null) {
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

    var camera = _camera.TryGetComponent<Camera>();
    VkCommandBuffer commandBuffer = VkCommandBuffer.Null;

    if (camera != null) {
      commandBuffer = Renderer.BeginFrame();
    }

    if (commandBuffer != VkCommandBuffer.Null && camera != null) {
      int frameIndex = Renderer.FrameIndex;
      _currentFrame.Camera = camera;
      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;
      _currentFrame.GlobalDescriptorSet = StorageCollection.GetDescriptor("GlobalStorage", frameIndex);
      _currentFrame.PointLightsDescriptorSet = StorageCollection.GetDescriptor("PointStorage", frameIndex);
      _currentFrame.ObjectDataDescriptorSet = StorageCollection.GetDescriptor("ObjectStorage", frameIndex);
      _currentFrame.JointsBufferDescriptorSet = StorageCollection.GetDescriptor("JointsStorage", frameIndex);
      _currentFrame.TextureManager = _textureManager;
      _currentFrame.ImportantEntity = _entities.Where(x => x.IsImportant).FirstOrDefault() ?? null!;

      // _currentFrame.DepthTexture = Renderer.Swapchain

      _ubo->Projection = _camera.TryGetComponent<Camera>()?.GetProjectionMatrix() ?? Matrix4x4.Identity;
      _ubo->View = _camera.TryGetComponent<Camera>()?.GetViewMatrix() ?? Matrix4x4.Identity;
      _ubo->CameraPosition = _camera.TryGetComponent<Transform>()?.Position ?? Vector3.Zero;
      _ubo->Fov = 60;
      _ubo->ImportantEntityPosition = _currentFrame.ImportantEntity?.TryGetComponent<Transform>()?.Position ?? Vector3.Zero;
      _ubo->ImportantEntityPosition.Z += 0.5f;
      _ubo->ImportantEntityDirection = _currentFrame.ImportantEntity?.TryGetComponent<Transform>()?.Forward ?? Vector3.Zero;
      _ubo->HasImportantEntity = _currentFrame.ImportantEntity != null ? 1 : 0;
      // _ubo->Fog = FogValue;
      _ubo->Fog = new(FogValue.X, Window.Extent.Width, Window.Extent.Height);
      _ubo->UseFog = UseFog ? 1 : 0;
      // _ubo->ImportantEntityPosition = new(6, 9);
      _ubo->ScreenSize = new(Window.Extent.Width, Window.Extent.Height);
      _ubo->HatchScale = Render3DSystem.HatchScale;

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

      // render
      // Renderer.BeginSwapchainRenderPass(commandBuffer);
      Renderer.BeginRendering(commandBuffer);

      _onRender?.Invoke();
      // _skybox?.Render(_currentFrame);
      Entity[] toUpdate = [.. _entities];
      Systems.UpdateSystems(toUpdate, _currentFrame);


      // Renderer.NextSwapchainSubpass(commandBuffer);

      // Renderer.EndSwapchainRenderPass(commandBuffer);
      // Renderer.BeginPostProcessRenderPass(commandBuffer);
      // Systems.PostProcessingSystem?.Render(FrameInfo);
      Systems.UpdateSecondPassSystems(toUpdate, _currentFrame);
      if (UseImGui) {
        GuiController.Update(Time.StopwatchDelta);
      }
      var updatable = _entities.Where(x => x.CanBeDisposed == false).ToArray();
      MasterRenderUpdate(updatable.GetScriptsAsSpan());
      _onGUI?.Invoke();
      if (UseImGui) {
        GuiController.Render(_currentFrame);
      }

      Renderer.EndRendering(commandBuffer);
      // Renderer.EndPostProcessRenderPass(commandBuffer);

      // Mutex.WaitOne();
      Renderer.EndFrame();
      // Mutex.ReleaseMutex();

      StorageCollection.CheckSize("ObjectStorage", frameIndex, Systems.Render3DSystem.LastKnownElemCount, _descriptorSetLayouts["ObjectData"]);
      StorageCollection.CheckSize("JointsStorage", frameIndex, (int)Systems.Render3DSystem.LastKnownSkinnedElemJointsCount, _descriptorSetLayouts["JointsBuffer"]);

      while (_reloadQueue.Count > 0) {
        var item = _reloadQueue.Dequeue();
        item.Dispose();
        item.Init(item.AABBFilter);
      }
    }

    Collect();

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
      int frameIndex = Renderer.FrameIndex;

      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;

      // Renderer.BeginSwapchainRenderPass(commandBuffer);
      // Renderer.NextSwapchainSubpass(commandBuffer);
      // Renderer.EndSwapchainRenderPass(commandBuffer);
      // Renderer.BeginPostProcessRenderPass(commandBuffer);
      Renderer.BeginRendering(commandBuffer);
      if (UseImGui) {
        GuiController.Update(Time.StopwatchDelta);
        _onAppLoading?.Invoke();
        GuiController.Render(_currentFrame);
      }
      // Renderer.EndPostProcessRenderPass(commandBuffer);
      Renderer.EndRendering(commandBuffer);

      Renderer.EndFrame();
    }
  }

  private void PerformCalculations() {
    Systems.UpdateCalculationSystems([.. GetEntities()]);
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
      // vkFreeCommandBuffers(
      //   Device.LogicalDevice,
      //   threadInfo.CommandPool,
      //   (uint)Renderer.MAX_FRAMES_IN_FLIGHT,
      //   cmdBfPtrEnd
      // );

      vkFreeCommandBuffers(
        Device.LogicalDevice,
        threadInfo.CommandPool,
        (uint)threadInfo.CommandBuffer.Length,
        cmdBfPtrEnd
      );
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
    // Renderer.BuildCommandBuffers(() => { });
    Mutex.ReleaseMutex();

    while (!_renderShouldClose) {
      // Logger.Warn("SPINNING " + !_renderShouldClose);
      if (Window.IsMinimalized) continue;

      Render(threadInfo);

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    Logger.Info("[RENDER LOOP] Closing Renderer");

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
      // entities[i].GetComponent<Sprite>()?.Dispose();

      // var u = entities[i].GetDrawables<IDrawable>();
      // foreach (var e in u) {
      //   var t = e as IDrawable;
      //   t?.Dispose();
      // }
      entities[i].DisposeEverything();
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
    if (VmaAllocator.IsNotNull) {
      vmaDestroyAllocator(VmaAllocator);
    }
    Device?.Dispose();
  }

  private void Collect() {
    if (_entities.Count == 0) return;
    for (short i = 0; i < _entities.Count; i++) {
      if (_entities[i].CanBeDisposed) {


        if (_entities[i].Collected) continue;

        _entities[i].Collected = true;
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
  // public Renderer Renderer { get; } = null!;
  public DynamicRenderer Renderer { get; } = null!;
  public VmaAllocator VmaAllocator { get; private set; }
  public FrameInfo FrameInfo => _currentFrame;
  public DirectionalLight DirectionalLight = DirectionalLight.New();
  public ImGuiController GuiController { get; private set; } = null!;
  public SystemCollection Systems { get; } = null!;
  public StorageCollection StorageCollection { get; private set; } = null!;
  public Scene CurrentScene { get; private set; } = null!;
  public bool UseImGui { get; } = true;
  public unsafe GlobalUniformBufferObject GlobalUbo => *_ubo;

  public const int MAX_POINT_LIGHTS_COUNT = 128;
}