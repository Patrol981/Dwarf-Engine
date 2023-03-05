using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Rendering;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public static class ApplicationState {
  public static Application s_App = null!;

  public static List<Vulkan.Buffer> s_BuffersToDestroyCandidates = new();
  public static List<VkDescriptorSet> s_DescSetsToDestroyCandidates = new();

  public unsafe static void ClearGarbage(DescriptorPool pool) {
    var buffArr = s_BuffersToDestroyCandidates.ToArray();
    for (int i = 0; i < buffArr.Length; i++) {
      buffArr[i].Dispose();
    }

    var descArr = s_DescSetsToDestroyCandidates.ToArray();
    fixed (VkDescriptorSet* ptr = descArr) {
      vkFreeDescriptorSets(s_App.Device.LogicalDevice, pool.GetVkDescriptorPool(), descArr.Length, ptr);
    }
  }
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
  public bool ReloadSimpleRenderSystem = false;

  private EventCallback? _onUpdate;
  private EventCallback? _onRender;
  private EventCallback? _onGUI;
  private EventCallback? _onLoad;

  private Window _window = null!;
  private Device _device = null!;
  private Renderer _renderer = null!;
  private TextureManager _textureManager = null!;
  private Render3DSystem _3dRenderSystem = null!;
  private DescriptorPool _globalPool = null!;
  private List<Entity> _entities = new();
  private Entity _camera = new();

  private Scene _currentScene = null!;

  // ubos
  private DescriptorSetLayout _globalSetLayout = null!;

  public Application() {
    _window = new Window(1200, 900);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);

    ApplicationState.s_App = this;
    _textureManager = new(_device);

    // Init();
    // Run();
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

    _3dRenderSystem = new(_device, _renderer, _renderer.GetSwapchainRenderPass(), _globalSetLayout.GetDescriptorSetLayout());
    _3dRenderSystem.SetupRenderData(_entities.ToArray(), ref _textureManager);

    while (!_window.ShouldClose) {
      glfwPollEvents();
      Time.StartTick();

      var sizes = _3dRenderSystem.CheckSizes(_entities.Count);
      if (!sizes || ReloadSimpleRenderSystem) {
        ReloadSimpleRenderSystem = false;
        ReloadRenderSystem();
      }

      float aspect = _renderer.AspectRatio;
      if (aspect != _camera.GetComponent<Camera>().Aspect) {
        _camera.GetComponent<Camera>().Aspect = aspect;
        _camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
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

        // ubo.LightPosition = new Vector3(-1, -2, -1);
        ubo.LightPosition = _camera.GetComponent<Transform>().Position;
        ubo.LightColor = new Vector4(1, 1, 1, 1);
        ubo.AmientLightColor = new Vector4(1f, 1f, 1f, 0.2f);

        uboBuffers[frameIndex].WriteToBuffer((IntPtr)(&ubo), (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());

        frameInfo.Camera = _camera.GetComponent<Camera>();
        frameInfo.CommandBuffer = commandBuffer;
        frameInfo.FrameIndex = frameIndex;
        frameInfo.GlobalDescriptorSet = globalDescriptorSets[frameIndex];
        frameInfo.TextureManager = _textureManager;

        // render
        _renderer.BeginSwapchainRenderPass(commandBuffer);
        _3dRenderSystem.RenderEntities(frameInfo, _entities.ToArray());
        _renderer.EndSwapchainRenderPass(commandBuffer);
        _renderer.EndFrame();

        // cleanup
        Collect();
      }

      _camera.GetComponent<Camera>().UpdateControls();

      GC.Collect(2, GCCollectionMode.Optimized, false);
      Time.EndTick();
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

  public void DestroyEntity(Entity entity) {
    entity.CanBeDisposed = true;
  }

  public void RemoveEntityRange(int index, int count) {
    _entities.RemoveRange(index, count);
  }

  public void ReloadRenderSystem() {
    _3dRenderSystem.Dispose();
    _3dRenderSystem = new(_device, _renderer, _renderer.GetSwapchainRenderPass(), _globalSetLayout.GetDescriptorSetLayout(), CurrentPipelineConfig);
    _3dRenderSystem.SetupRenderData(_entities.ToArray(), ref _textureManager);
  }

  public async void Init() {
    _globalPool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)_renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)_renderer.MAX_FRAMES_IN_FLIGHT)
      .Build();

    float aspect = _renderer.AspectRatio;
    _camera.AddComponent(new Transform(new Vector3(0, 0, 0)));
    _camera.AddComponent(new Camera(50, aspect));
    _camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
    _camera.GetComponent<Camera>()._yaw = 1.3811687f;
    _camera.AddComponent(new FreeCameraController());

    CameraState.SetCamera(_camera.GetComponent<Camera>());
    CameraState.SetCameraEntity(_camera);

    await LoadTextures();
    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.LoadedTextures.Count}");
  }

  public void MultiThreadedTextureLoad(List<List<string>> paths) {
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
  }

  private async Task<Task> LoadTextures() {
    _currentScene.LoadTextures();
    await LoadTexturesAsSeparateThreads(_currentScene.GetTexturePaths());
    return Task.CompletedTask;
  }

  private Task LoadTexturesAsSeparateThreads(List<List<string>> paths) {
    MultiThreadedTextureLoad(paths);
    Logger.Info("Done Loading Textures");
    return Task.CompletedTask;
  }

  private Task LoadEntities() {
    _currentScene.LoadEntities();
    _entities.AddRange(_currentScene.GetEntities());
    return Task.CompletedTask;
  }

  private void Cleanup() {
    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      entities[i].GetComponent<Model>()?.Dispose();
    }
    _textureManager?.Dispose();
    _globalSetLayout.Dispose();
    _globalPool.Dispose();
    _3dRenderSystem?.Dispose();
    _renderer?.Dispose();
    _window?.Dispose();
    _device?.Dispose();
  }

  private void Collect() {
    for (int i = 0; i < _entities.Count; i++) {
      if (_entities[i].CanBeDisposed) {
        _entities[i].GetComponent<Model>().Dispose();
        ApplicationState.s_App.RemoveEntity(_entities[i]);
      }
    }
  }

  public Device Device => _device;
  public TextureManager TextureManager => _textureManager;
}