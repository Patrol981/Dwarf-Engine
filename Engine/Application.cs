using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Rendering;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Engine.Rendering.UI.FontReader;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using DwarfEngine.Engine.Rendering;
using DwarfEngine.Engine.Rendering.UI;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public static class ApplicationState {
  public static Application Instance = null!;
}

public class Application {
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
  private SystemCollection _systems = new();
  private DescriptorPool _globalPool = null!;

  private List<Entity> _entities = new();
  private List<FontFile> _loadedFonts = new();
  private Entity _camera = new();

  private Scene _currentScene = null!;

  // ubos
  private DescriptorSetLayout _globalSetLayout = null!;
  private readonly SystemCreationFlags _systemCreationFlags;

  public Application(string appName = "Dwarf Vulkan", SystemCreationFlags systemCreationFlags = SystemCreationFlags.Renderer3D) {
    _window = new Window(1200, 900, appName);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);

    ApplicationState.Instance = this;
    _textureManager = new(_device);

    _systemCreationFlags = systemCreationFlags;
  }

  public void SetupScene(Scene scene) {
    _currentScene = scene;
  }

  public unsafe void Run() {
    Vulkan.Buffer[] uboBuffers = new Vulkan.Buffer[_renderer.MAX_FRAMES_IN_FLIGHT];
    for (int i = 0; i < uboBuffers.Length; i++) {
      uboBuffers[i] = new(
        _device,
        (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>(),
        1,
        VkBufferUsageFlags.UniformBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );
      uboBuffers[i].Map((ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());
    }

    _globalSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    VkDescriptorSet[] globalDescriptorSets = new VkDescriptorSet[_renderer.MAX_FRAMES_IN_FLIGHT];
    for (int i = 0; i < globalDescriptorSets.Length; i++) {
      var bufferInfo = uboBuffers[i].GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());
      var writer = new DescriptorWriter(_globalSetLayout, _globalPool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out globalDescriptorSets[i]);
    }

    SetupSystems(_systemCreationFlags, _device, _renderer, _globalSetLayout, null!);
    _systems.GetRender3DSystem()?.SetupRenderData(Entity.Distinct<Model>(_entities).ToArray(), ref _textureManager);
    _systems.GetRender2DSystem()?.Setup(Entity.Distinct<Sprite>(_entities).ToArray(), ref _textureManager);
    _systems.GetRenderUISystem()?.SetupUIData(Entity.DistinctInterface<IUIElement>(_entities).ToArray(), ref _textureManager);
    _systems.GetPhysicsSystem()?.Init(_entities.ToArray());

    _onLoad?.Invoke();

    while (!_window.ShouldClose) {
      glfwPollEvents();
      Time.Tick();

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
        FrameInfo frameInfo = new();

        // update
        GlobalUniformBufferObject ubo = new();
        // ubo.LightDirection = new Vector3(1, -3, -1).Normalized();
        ubo.Projection = _camera.GetComponent<Camera>().GetProjectionMatrix();
        ubo.View = _camera.GetComponent<Camera>().GetViewMatrix();

        // ubo.LightPosition = new Vector3(-1, -1, 0);
        ubo.LightPosition = _camera.GetComponent<Transform>().Position;
        ubo.LightColor = new Vector4(1f, 1f, 1f, 1f);
        ubo.AmientLightColor = new Vector4(1f, 1f, 1f, 1f);
        ubo.CameraPosition = _camera.GetComponent<Transform>().Position;

        uboBuffers[frameIndex].WriteToBuffer((IntPtr)(&ubo), (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());

        frameInfo.Camera = _camera.GetComponent<Camera>();
        frameInfo.CommandBuffer = commandBuffer;
        frameInfo.FrameIndex = frameIndex;
        frameInfo.GlobalDescriptorSet = globalDescriptorSets[frameIndex];
        frameInfo.TextureManager = _textureManager;

        // render
        _renderer.BeginSwapchainRenderPass(commandBuffer);
        _onRender?.Invoke();
        _systems.UpdateSystems(_entities.ToArray(), frameInfo);
        _renderer.EndSwapchainRenderPass(commandBuffer);
        _renderer.EndFrame();

        // cleanup
        Collect();
      }

      _camera.GetComponent<Camera>().UpdateControls();
      _onUpdate?.Invoke();
      _onGUI?.Invoke();

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    var result = vkDeviceWaitIdle(_device.LogicalDevice);
    if (result != VkResult.Success) {
      Logger.Error(result.ToString());
    }

    for (int i = 0; i < uboBuffers.Length; i++) {
      uboBuffers[i].Dispose();
    }
    Cleanup();
  }

  public void SetCamera(Entity camera) {
    _camera = camera;
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
    _entities.Add(entity);
  }

  public List<Entity> GetEntities() {
    return _entities;
  }

  public void RemoveEntityAt(int index) {
    _entities.RemoveAt(index);
  }

  public void RemoveEntity(Entity entity) {
    _entities.Remove(entity);
  }

  public void RemoveEntity(Guid id) {
    var target = _entities.Where((x) => x.EntityID == id).First();
    _entities.Remove(target);
  }

  public void DestroyEntity(Entity entity) {
    entity.CanBeDisposed = true;
  }

  public void RemoveEntityRange(int index, int count) {
    _entities.RemoveRange(index, count);
  }

  public async void Init() {
    Console.WriteLine(DateTime.UtcNow.ToString());

    _globalPool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)_renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)_renderer.MAX_FRAMES_IN_FLIGHT)
      .Build();

    await LoadTextures();
    await LoadEntities();
    await LoadFonts();

    Console.WriteLine(DateTime.UtcNow.ToString());
    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.LoadedTextures.Count}");
    Logger.Info($"Loaded fonts: {_loadedFonts.Count}");
  }

  public void MultiThreadedTextureLoad(List<List<string>> paths) {
    var startTime = DateTime.UtcNow;
    List<Thread> threads = new();
    List<TextureThread> textureThreads = new();
    List<List<Texture>> textures = new();
    for (int i = 0; i < paths.Count; i++) {
      textures.Add(Texture.InitTextures(ref _device, paths[i].ToArray()).ToList());
      textureThreads.Add(new TextureThread(ref _device, textures[i].ToArray(), paths[i].ToArray()));
      threads.Add(new(new ThreadStart(textureThreads[i].Process)));
    }

    for (int i = 0; i < paths.Count; i++) {
      threads[i].Start();
    }

    for (int i = 0; i < paths.Count; i++) {
      threads[i].Join();
    }

    for (int i = 0; i < paths.Count; i++) {
      _textureManager.AddRange(textures[i].ToArray());
    }
    var endTime = DateTime.Now;
  }

  private async Task<Task> LoadTextures() {
    _currentScene.LoadTextures();
    await LoadTexturesAsSeparateThreads(_currentScene.GetTexturePaths());
    return Task.CompletedTask;
  }

  public Task LoadTexturesAsSeparateThreads(List<List<string>> paths) {
    MultiThreadedTextureLoad(paths);
    Logger.Info("Done Loading Textures");
    return Task.CompletedTask;
  }

  private Task LoadEntities() {
    _currentScene.LoadEntities();
    _entities.AddRange(_currentScene.GetEntities());
    return Task.CompletedTask;
  }

  private Task LoadFonts() {
    _currentScene.LoadFonts();
    _loadedFonts.AddRange(_currentScene.GetFonts());
    return Task.CompletedTask;
  }

  private void Cleanup() {
    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      entities[i].GetComponent<Model>()?.Dispose();
      entities[i].GetComponent<Sprite>()?.Dispose();
      var e = entities[i].GetDrawable<IUIElement>() as IUIElement;
      e?.Dispose();
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
    // Colect Models
    var models = Entity.Distinct<Model>(_entities);
    for (int i = 0; i < models.Length; i++) {
      if (models[i].CanBeDisposed) {
        models[i].GetComponent<Model>().Dispose();
        ApplicationState.Instance.RemoveEntity(models[i].EntityID);
      }
    }
  }

  public Device Device => _device;
  public Window Window => _window;
  public TextureManager TextureManager => _textureManager;
  public Renderer Renderer => _renderer;
  public List<FontFile> LoadedFonts => _loadedFonts;
}