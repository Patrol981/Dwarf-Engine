using System.Net.Mime;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
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
  private Pipeline _pipeline = null!;
  private Device _device = null!;
  private Renderer _renderer = null!;
  private VkPipelineLayout _pipelineLayout;
  private List<Entity> _entities = new();

  // private Model _model;
  // private Entity _testEntity;

  public Application() {
    _window = new Window(1200, 900);
    _device = new Device(_window);
    _renderer = new Renderer(_window, _device);
    LoadEntities();
    CreatePipelineLayout();
    CreatePipeline();
    Run();
  }

  public void Run() {
    while (!_window.ShouldClose) {
      glfwPollEvents();

      var commandBuffer = _renderer.BeginFrame();
      if (commandBuffer != VkCommandBuffer.Null) {
        _renderer.BeginSwapchainRenderPass(commandBuffer);
        RenderEntities(commandBuffer);
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

  private void LoadEntities() {
    Vertex[] vertices = new Vertex[3];
    vertices[0] = new();
    vertices[1] = new();
    vertices[2] = new();

    vertices[0].Position = new System.Numerics.Vector2(0.0f, -0.5f);
    vertices[1].Position = new System.Numerics.Vector2(0.5f, 0.5f);
    vertices[2].Position = new System.Numerics.Vector2(-0.5f, 0.5f);

    vertices[0].Color = new Vector3(1.0f, 0.0f, 0.0f);
    vertices[1].Color = new Vector3(0.0f, 1.0f, 0.0f);
    vertices[2].Color = new Vector3(0.0f, 0.0f, 1.0f);

    var en = new Entity();
    en.AddComponent(new Model(_device, vertices));
    en.AddComponent(new Material(new Vector3(0.1f, 0.1f, 0.1f)));
    en.AddComponent(new Transform2D(new Vector2(0.2f, 0.0f)));
    AddEntity(en);

    var en2 = new Entity();
    en2.AddComponent(new Model(_device, vertices));
    en2.AddComponent(new Material(new Vector3(0.0f, 0.0f, 0.1f)));
    en2.AddComponent(new Transform2D(new Vector2(0f, 0.0f)));
    AddEntity(en2);

    var en3 = new Entity();
    en3.AddComponent(new Model(_device, vertices));
    en3.AddComponent(new Material(new Vector3(0.1f, 0.0f, 0.0f)));
    en3.AddComponent(new Transform2D(new Vector2(-0.2f, 0.2f), new Vector3(2, 1, 1)));
    en3.GetComponent<Transform2D>().Rotation = 0.25f * (MathF.PI * 2);
    AddEntity(en3);

    // _model = new Model(_device, vertices);
  }

  private void CreatePipelineLayout() {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<SimplePushConstantData>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
    pipelineInfo.setLayoutCount = 0;
    pipelineInfo.pSetLayouts = null;
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private void CreatePipeline() {
    _pipeline?.Dispose();
    PipelineConfigInfo configInfo = new();
    var pipelineConfig = Pipeline.DefaultConfigInfo(configInfo);
    pipelineConfig.RenderPass = _renderer.GetSwapchainRenderPass();
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "vertex", "fragment", pipelineConfig);
  }
  private void RenderEntities(VkCommandBuffer commandBuffer) {
    _pipeline.Bind(commandBuffer);

    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      //var x = entities[i].GetComponent<Transform2D>().Rotation + 0.01f;
      //entities[i].GetComponent<Transform2D>().Rotation = x % (MathF.PI * 2);

      var pushConstantData = new SimplePushConstantData();
      pushConstantData.Transform = entities[i].GetComponent<Transform2D>().Matrix4;
      pushConstantData.Offset = entities[i].GetComponent<Transform2D>().Translation;
      pushConstantData.Color = entities[i].GetComponent<Material>().GetColor();

      vkCmdPushConstants(
        commandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<SimplePushConstantData>(),
        &pushConstantData
      );

      entities[i].GetComponent<Model>()?.Bind(commandBuffer);
      entities[i].GetComponent<Model>()?.Draw(commandBuffer);
    }
  }

  private void Cleanup() {
    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      entities[i].GetComponent<Model>()?.Dispose();
    }
    // _testEntity.GetComponent<Model>()?.Dispose();
    // _model.Dispose();
    _renderer?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
    _window?.Dispose();
    _device?.Dispose();
  }
}