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
  private SimpleRenderSystem _simpleRender = null!;
  private DescriptorPool _globalPool = null!;
  private List<Entity> _entities = new();
  private Entity _camera = new();

  // ubos
  private DescriptorSetLayout _globalSetLayout = null!;

  public Application() {
    _window = new Window(1200, 900);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);

    ApplicationState.s_App = this;

    Init();
    Run();
  }

  public void Run() {
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

    _simpleRender = new(_device, _renderer, _renderer.GetSwapchainRenderPass(), _globalSetLayout.GetDescriptorSetLayout());
    _simpleRender.SetupRenderData(_entities.ToArray(), ref _textureManager);

    var elasped = 0.0f;
    var testState = true;
    var totalSpawned = 0;

    while (!_window.ShouldClose) {
      glfwPollEvents();
      Time.StartTick();

      var sizes = _simpleRender.CheckSizes(_entities.Count);
      if (!sizes || ReloadSimpleRenderSystem) {
        ReloadSimpleRenderSystem = false;
        ReloadRenderSystem();
        // _simpleRender.Dispose();
        // _simpleRender = new(_device, _renderer, _renderer.GetSwapchainRenderPass(), _globalSetLayout.GetDescriptorSetLayout());
        // _simpleRender.SetupRenderData(_entities.Count);
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
        // uboBuffers[frameIndex].Flush((ulong)Unsafe.SizeOf<GlobalUniformBufferObject>());

        frameInfo.Camera = _camera.GetComponent<Camera>();
        frameInfo.CommandBuffer = commandBuffer;
        frameInfo.FrameIndex = frameIndex;
        frameInfo.GlobalDescriptorSet = globalDescriptorSets[frameIndex];
        frameInfo.TextureManager = _textureManager;

        // render
        _renderer.BeginSwapchainRenderPass(commandBuffer);
        _simpleRender.RenderEntities(frameInfo, _entities.ToArray());
        _renderer.EndSwapchainRenderPass(commandBuffer);
        _renderer.EndFrame();

        // cleanup
        Collect();
      }

      _camera.GetComponent<Camera>().UpdateControls();

      if (elasped > 1.0f) {
        //elasped = 0.0f;
        //continue;
        if (testState) {
          var box2 = new Entity();
          box2.AddComponent(new GenericLoader().LoadModel(ApplicationState.s_App.Device, "./Models/colored_cube.obj"));
          box2.GetComponent<Model>().BindToTexture(ref _textureManager, "viking_room/viking_room.png", true);
          box2.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
          box2.AddComponent(new Transform(new Vector3(1.0f, -7.0f, -1f)));
          box2.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
          box2.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
          ApplicationState.s_App.AddEntity(box2);
          testState = !testState;
          elasped = 0.0f;
          totalSpawned += 1;
        } else {
          var count = ApplicationState.s_App.GetEntities().Count - 1;
          var entities = ApplicationState.s_App.GetEntities();
          entities[count].CanBeDisposed = true;
          testState = !testState;
          elasped = 0.0f;
        }
      }

      GC.Collect(2, GCCollectionMode.Optimized, false);
      Time.EndTick();
      elasped += Time.DeltaTime;
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
    _simpleRender.Dispose();
    _simpleRender = new(_device, _renderer, _renderer.GetSwapchainRenderPass(), _globalSetLayout.GetDescriptorSetLayout(), CurrentPipelineConfig);
    _simpleRender.SetupRenderData(_entities.ToArray(), ref _textureManager);
  }

  private void Init() {
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

    _textureManager = new(_device);

    LoadTextures();
    LoadEntities();
  }

  private void LoadTextures() {
    _textureManager.AddTextureFromLocal("base/no_texture.png");
    _textureManager.AddTextureFromLocal("viking_room/viking_room.png");

    _textureManager.AddTextureFromLocal("dwarf_test_model/_01.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_02.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_03.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_04.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_05.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_06.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_07.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_08.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_09.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_10.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_11.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_12.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_13.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_14.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_15.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_16.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_17.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_18.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_19.png");
    _textureManager.AddTextureFromLocal("dwarf_test_model/_20.png");

  }

  private void LoadEntities() {
    Console.WriteLine(Directory.GetCurrentDirectory());

    var en = new Entity();
    en.AddComponent(new GenericLoader().LoadModel(_device, "./Models/dwarf_test_model.obj"));
    // en.GetComponent<Model>().BindToTexture(ref _textureManager, "base/no_texture.png", true);

    string[] texturesToLoad = {
      "dwarf_test_model/_01.png", // mouth
      "dwarf_test_model/_02.png", // eyes
      "dwarf_test_model/_03.png", // eye mid
      "dwarf_test_model/_04.png", // face
      "dwarf_test_model/_06.png", // possibly face shadow ?
      "dwarf_test_model/_07.png", // eyebrows
      "dwarf_test_model/_08.png", // eyeleashes
      "dwarf_test_model/_09.png", // eyeleashes
      "dwarf_test_model/_10.png", // body
      "dwarf_test_model/_12.png", // hair base
      "dwarf_test_model/_13.png", // outfit
      "dwarf_test_model/_14.png", // outfit
      "dwarf_test_model/_15.png", // outfit
      "dwarf_test_model/_16.png", // outfit
      "dwarf_test_model/_17.png", // outfit
      "dwarf_test_model/_18.png", // outfit
      "dwarf_test_model/_19.png", // hair
    };

    en.GetComponent<Model>().BindMultipleModelPartsToTextures(ref _textureManager, texturesToLoad, true);
    en.AddComponent(new Material(new Vector3(1f, 0.7f, 0.9f)));
    en.AddComponent(new Transform(new Vector3(0.0f, 0f, 0f)));
    en.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
    en.GetComponent<Transform>().Rotation = new(180f, 0f, 0);
    Logger.Info(en.GetComponent<Model>().MeshsesCount.ToString());
    AddEntity(en);

    var vase = new Entity();
    vase.AddComponent(new GenericLoader().LoadModel(_device, "./Models/flat_vase.obj"));
    vase.GetComponent<Model>().BindToTexture(ref _textureManager, "base/no_texture.png", true);
    vase.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    vase.AddComponent(new Transform(new Vector3(0.5f, 0f, -2f)));
    vase.GetComponent<Transform>().Scale = new(3f, 3f, 3f);
    vase.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
    Logger.Info(vase.GetComponent<Model>().MeshsesCount.ToString());
    AddEntity(vase);

    var vase2 = new Entity();
    vase2.AddComponent(new GenericLoader().LoadModel(_device, "./Models/smooth_vase.obj"));
    vase2.GetComponent<Model>().BindToTexture(ref _textureManager, "base/no_texture.png", true);
    vase2.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    vase2.AddComponent(new Transform(new Vector3(.0f, 0f, 3.5f)));
    vase2.GetComponent<Transform>().Scale = new(3f, 3f, 3f);
    vase2.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
    AddEntity(vase2);

    var room = new Entity();
    room.AddComponent(new GenericLoader().LoadModel(_device, "./Models/viking_room.obj"));
    room.GetComponent<Model>().BindToTexture(ref _textureManager, "viking_room/viking_room.png", true);
    room.AddComponent(new Material(new Vector3(1.0f, 1.0f, 1.0f)));
    room.AddComponent(new Transform(new Vector3(4.5f, 0, 1f)));
    room.GetComponent<Transform>().Rotation = new Vector3(90, 225, 0);
    room.GetComponent<Transform>().Scale = new Vector3(3, 3, 3);
    room.Name = "viking room";
    AddEntity(room);

    var floor = new Entity();
    floor.AddComponent(new GenericLoader().LoadModel(_device, "./Models/cube.obj"));
    floor.GetComponent<Model>().BindToTexture(ref _textureManager, "base/no_texture.png", true);
    floor.AddComponent(new Material(new Vector3(0.5f, 1, 0.5f)));
    floor.AddComponent(new Transform(new Vector3(0f, 0.1f, 0f)));
    floor.GetComponent<Transform>().Rotation = new Vector3(0, 0, 0);
    floor.GetComponent<Transform>().Scale = new Vector3(7, 0.1f, 7);
    AddEntity(floor);
  }

  private void Cleanup() {
    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      entities[i].GetComponent<Model>()?.Dispose();
    }
    _textureManager?.Dispose();
    _globalSetLayout.Dispose();
    _globalPool.Dispose();
    _simpleRender?.Dispose();
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
}