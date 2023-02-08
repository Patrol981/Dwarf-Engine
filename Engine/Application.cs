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
  // private Camera _camera;
  private Entity _camera = new();

  // private Model _model;
  // private Entity _testEntity;

  public Application() {
    _window = new Window(1200, 900);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);
    _simpleRender = new(_device, _renderer.GetSwapchainRenderPass());

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
        VkMemoryPropertyFlags.HostVisible,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );
      uboBuffers[i].Map();
    }

    while (!_window.ShouldClose) {
      glfwPollEvents();
      Time.StartTick();

      float aspect = _renderer.AspectRatio;
      if (aspect != _camera.GetComponent<Camera>().Aspect) {
        _camera.GetComponent<Camera>().Aspect = aspect;
        _camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
      }

      // _camera.GetComponent<Camera>().UpdateVectors();

      var commandBuffer = _renderer.BeginFrame();
      if (commandBuffer != VkCommandBuffer.Null) {
        int frameIndex = _renderer.GetFrameIndex();
        FrameInfo frameInfo = new();

        // update
        GlobalUniformBufferObject ubo = new();
        ubo.Model = Matrix4.Identity;
        ubo.View = Matrix4.Identity;
        ubo.Projection = Matrix4.Identity;
        ubo.LightDirection = new Vector3(1, -3, -1).Normalized();
        ubo.Projection = _camera.GetComponent<Camera>().GetProjectionMatrix();
        ubo.View = _camera.GetComponent<Camera>().GetViewMatrix();

        uboBuffers[frameIndex].WriteToBuffer((IntPtr)(&ubo));
        uboBuffers[frameIndex].Flush();
        //globalUBO.WrtieToIndex(&ubo, frameIndex);
        //globalUBO.FlushIndex(frameIndex);

        frameInfo.Camera = _camera.GetComponent<Camera>();
        frameInfo.CommandBuffer = commandBuffer;
        frameInfo.FrameIndex = frameIndex;

        // render
        _renderer.BeginSwapchainRenderPass(commandBuffer);
        _simpleRender.RenderEntities(frameInfo, _entities.ToArray());
        _renderer.EndSwapchainRenderPass(commandBuffer);
        _renderer.EndFrame();
      }

      // _camera.GetComponent<Transform>().Position.X += 0.001f;
      // _camera.GetComponent<Camera>()._yaw -= 0.0001f;
      //Console.WriteLine(_camera.GetComponent<Camera>()._yaw);

      MoveCam();

      Time.EndTick();
    }

    var result = vkDeviceWaitIdle(_device.LogicalDevice);
    if (result != VkResult.Success) {
      Logger.Error(result.ToString());
    }

    // globalUBO.Dispose();
    for (int i = 0; i < uboBuffers.Length; i++) {
      uboBuffers[i].Dispose();
    }
    Cleanup();
  }

  private void MoveCam() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);
    } else {
      var deltaX = (float)MouseState.GetInstance().MousePosition.X - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)MouseState.GetInstance().MousePosition.Y - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(MouseState.GetInstance().MousePosition);

      if (WindowState.s_MouseCursorState == InputValue.GLFW_CURSOR_DISABLED) {
        _camera.GetComponent<Camera>().Yaw += deltaX * CameraState.GetSensitivity();
        _camera.GetComponent<Camera>().Pitch += deltaY * CameraState.GetSensitivity();
      }

      if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_D) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        _camera.GetComponent<Transform>().Position += _camera.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_A) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        _camera.GetComponent<Transform>().Position -= _camera.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_S) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        _camera.GetComponent<Transform>().Position -= _camera.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_W) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        _camera.GetComponent<Transform>().Position += _camera.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_LEFT_SHIFT) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        _camera.GetComponent<Transform>().Position -= _camera.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_SPACE) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
        _camera.GetComponent<Transform>().Position += _camera.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      //if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_F) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
      //WindowState.FocusOnWindow();
      //}
    }


  }

  public void AddEntity(Entity entity) {
    _entities.Add(entity);
  }

  private void Init() {
    float aspect = _renderer.AspectRatio;
    _camera.AddComponent(new Transform(new Vector3(0, 0, 0)));
    _camera.AddComponent(new Camera(50, aspect));
    _camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
    _camera.GetComponent<Camera>()._yaw = 1.3811687f;

    CameraState.SetCamera(_camera.GetComponent<Camera>());
    CameraState.SetCameraEntity(_camera);

    LoadEntities();
  }


  private void LoadEntities() {
    Console.WriteLine(Directory.GetCurrentDirectory());

    var en = new Entity();
    //en.AddComponent(GetCube(new Vector3(0, 0, 0)));
    // en.AddComponent(new Model(_device, GenericLoader.LoadModel("./Models/dwarf_test_model.obj")));
    en.AddComponent(new GenericLoader().LoadModel(_device, "./Models/dwarf_test_model.obj"));
    en.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    en.AddComponent(new Transform(new Vector3(0.0f, 2f, 2f)));
    // en.GetComponent<Transform>().Translation = new(0f, -1.5f, 2f);
    en.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
    en.GetComponent<Transform>().Rotation = new(180f, 0f, 0);
    AddEntity(en);

    var box = new Entity();
    box.AddComponent(new GenericLoader().LoadModel(_device, "./Models/cube.obj"));
    box.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    box.AddComponent(new Transform(new Vector3(3.0f, 0f, 5f)));
    box.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
    box.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
    AddEntity(box);

    var box2 = new Entity();
    box2.AddComponent(new GenericLoader().LoadModel(_device, "./Models/colored_cube.obj"));
    box2.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    box2.AddComponent(new Transform(new Vector3(1.0f, -3.0f, -1f)));
    box2.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
    box2.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
    AddEntity(box2);

    var vase = new Entity();
    vase.AddComponent(new GenericLoader().LoadModel(_device, "./Models/flat_vase.obj"));
    vase.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    vase.AddComponent(new Transform(new Vector3(0.5f, 1f, -1f)));
    vase.GetComponent<Transform>().Scale = new(3f, 3f, 3f);
    vase.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
    AddEntity(vase);

    var vase2 = new Entity();
    vase2.AddComponent(new GenericLoader().LoadModel(_device, "./Models/smooth_vase.obj"));
    vase2.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    vase2.AddComponent(new Transform(new Vector3(.0f, .5f, 2.5f)));
    vase2.GetComponent<Transform>().Scale = new(3f, 3f, 3f);
    vase2.GetComponent<Transform>().Rotation = new(0f, 0f, 0);
    AddEntity(vase2);
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