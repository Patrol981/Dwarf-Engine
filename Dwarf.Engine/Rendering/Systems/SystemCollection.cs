using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Rendering.Systems;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Engine.Rendering;
public class SystemCollection : IDisposable {
  // Render Systems
  private Render3DSystem? _render3DSystem;
  private Render2DSystem? _render2DSystem;
  private RenderUISystem? _renderUISystem;
  private RenderDebugSystem? _renderDebugSystem;
  private PointLightSystem? _pointLightSystem;

  // TODO : More canvases in the future?
  private Canvas? _canvas = null;

  // Calculation Systems
  private PhysicsSystem? _physicsSystem;
  private Thread? _physicsThread;
  private readonly object _renderLock = new object();

  public bool Reload3DRenderSystem = false;
  public bool Reload2DRenderSystem = false;
  public bool ReloadUISystem = false;

  public void UpdateSystems(Entity[] entities, FrameInfo frameInfo) {
    lock (_renderLock) {
      _render3DSystem?.Render(frameInfo, Entity.DistinctInterface<IRender3DElement>(entities).ToArray());
      _render2DSystem?.Render(frameInfo, Entity.Distinct<Sprite>(entities).ToArray());
      _renderDebugSystem?.Render(frameInfo, Entity.DistinctInterface<IDebugRender3DObject>(entities).ToArray());
      _pointLightSystem?.Render(frameInfo);
      _renderUISystem?.DrawUI(frameInfo, _canvas ?? throw new Exception("Canvas cannot be null"));
    }
  }

  public Task UpdateCalculationSystems(Entity[] entities) {
    if (_physicsSystem != null) {
      _physicsSystem!.Tick(entities);
    }
    return Task.CompletedTask;
  }

  private void RenderThread() {
    lock (_renderLock) {
      var frameInfo = Application.Instance.FrameInfo;
      var entities = Application.Instance.GetEntities();

      _render3DSystem?.Render(frameInfo, Entity.DistinctInterface<IRender3DElement>(entities).ToArray());
      _render2DSystem?.Render(frameInfo, Entity.Distinct<Sprite>(entities).ToArray());
      _renderDebugSystem?.Render(frameInfo, Entity.DistinctInterface<IDebugRender3DObject>(entities).ToArray());
      _renderUISystem?.DrawUI(frameInfo, _canvas ?? throw new Exception("Canvas cannot be null"));
    }
  }

  public void ValidateSystems(
    ReadOnlySpan<Entity> entities,
    VulkanDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    PipelineConfigInfo pipelineConfigInfo,
    ref TextureManager textureManager
  ) {
    if (_render3DSystem != null) {
      var modelEntities = Entity.DistinctInterface<IRender3DElement>(entities).ToArray();
      if (modelEntities.Length < 1) return;
      var sizes = _render3DSystem.CheckSizes(modelEntities);
      var textures = _render3DSystem.CheckTextures(modelEntities);
      if (!sizes || !textures || Reload3DRenderSystem) {
        Reload3DRenderSystem = false;
        Reload3DRenderer(device, renderer, globalLayout, ref textureManager, pipelineConfigInfo, entities);
      }
    }

    if (_render2DSystem != null) {
      var spriteEntities = Entity.Distinct<Sprite>(entities).ToArray();
      if (spriteEntities.Length < 1) return;
      var sizes = _render2DSystem.CheckSizes(spriteEntities);
      var textures = _render2DSystem.CheckTextures(spriteEntities);
      if (!sizes || !textures || Reload2DRenderSystem) {
        Reload2DRenderSystem = false;
        Reload2DRenderer(device, renderer, globalLayout, ref textureManager, pipelineConfigInfo, entities);
      }
    }

    if (_renderUISystem != null) {
      // var uiEntities = Entity.Distinct<TextField>(entities).ToArray();
      // var uiEntities = Entity.DistinctInterface<IUIElement>(entities).ToArray();
      var canvasEntities = _canvas!.GetUI();
      if (canvasEntities.Length < 1) return;
      var sizes = _renderUISystem.CheckSizes(canvasEntities, _canvas);
      var textures = _renderUISystem.CheckTextures(canvasEntities);
      if (!sizes || !textures || ReloadUISystem) {
        ReloadUISystem = false;
        ReloadUIRenderer(device, renderer, globalLayout, ref textureManager, pipelineConfigInfo);
      }
    }
  }

  public void ReloadSystems(
    VulkanDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    PipelineConfigInfo pipelineConfigInfo,
    ReadOnlySpan<Entity> entities,
    Canvas canvas,
    ref TextureManager textureManager
  ) {

    if (_render3DSystem != null) {
      _render3DSystem.Dispose();
      _render3DSystem = new Render3DSystem(device, renderer, globalLayout, pipelineConfigInfo);
    }

    if (_render2DSystem != null) {
      _render2DSystem.Dispose();
      _render2DSystem = new Render2DSystem(device, renderer, globalLayout, pipelineConfigInfo);
    }

    if (_renderUISystem != null) {
      _renderUISystem.Dispose();
      _renderUISystem = new RenderUISystem(device, renderer, globalLayout, pipelineConfigInfo);
    }

    SetupRenderDatas(entities, canvas, ref textureManager, renderer);
  }

  public void SetupRenderDatas(ReadOnlySpan<Entity> entities, Canvas canvas, ref TextureManager textureManager, Renderer renderer) {
    if (_render3DSystem != null) {
      _render3DSystem.Setup(Entity.DistinctInterface<IRender3DElement>(entities).ToArray(), ref textureManager);
    }

    if (_render2DSystem != null) {
      _render2DSystem.Setup(Entity.Distinct<Sprite>(entities).ToArray(), ref textureManager);
    }

    if (_renderUISystem != null) {
      _renderUISystem.Setup(canvas, ref textureManager);
    }
  }

  public void Reload3DRenderer(
    VulkanDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig,
    ReadOnlySpan<Entity> entities
  ) {
    _render3DSystem?.Dispose();
    _render3DSystem = new Render3DSystem(
      device,
      renderer,
      globalLayout,
      pipelineConfig
    );
    _render3DSystem?.Setup(Entity.DistinctInterface<IRender3DElement>(entities).ToArray(), ref textureManager);
  }

  public void Reload2DRenderer(
    VulkanDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig,
    ReadOnlySpan<Entity> entities
  ) {
    _render2DSystem?.Dispose();
    _render2DSystem = new Render2DSystem(
      device,
      renderer,
      globalLayout,
      pipelineConfig
    );
    _render2DSystem?.Setup(Entity.Distinct<Sprite>(entities).ToArray(), ref textureManager);
  }

  public void ReloadUIRenderer(
    VulkanDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig
  ) {
    _renderUISystem?.Dispose();
    _renderUISystem = new RenderUISystem(
      device,
      renderer,
      globalLayout,
      pipelineConfig
    );
    _renderUISystem?.Setup(_canvas ?? throw new Exception("Canvas cannot be null"), ref textureManager);
  }

  public Render3DSystem Render3DSystem {
    get { return _render3DSystem ?? null!; }
    set { _render3DSystem = value; }
  }

  public Render2DSystem Render2DSystem {
    get { return _render2DSystem ?? null!; }
    set { _render2DSystem = value; }
  }

  public RenderUISystem RenderUISystem {
    get { return _renderUISystem ?? null!; }
    set { _renderUISystem = value; }
  }

  public PhysicsSystem PhysicsSystem {
    get { return _physicsSystem ?? null!; }
    set {
      _physicsSystem = value;
      // _physicsThread = new Thread(new ParameterizedThreadStart(PhysicsSystem.Calculate!));
    }
  }

  public Thread PhysicsThread {
    set { _physicsThread = value; }
    get { return _physicsThread ?? null!; }
  }

  public RenderDebugSystem RenderDebugSystem {
    get { return _renderDebugSystem ?? null!; }
    set { _renderDebugSystem = value; }
  }

  public PointLightSystem PointLightSystem {
    get { return _pointLightSystem ?? null!; }
    set { _pointLightSystem = value; }
  }

  public Canvas Canvas {
    get { return _canvas ?? null!; }
    set { _canvas = value; }
  }

  public void Dispose() {
    _render3DSystem?.Dispose();
    _render2DSystem?.Dispose();
    _canvas?.Dispose();
    _renderUISystem?.Dispose();
    _physicsSystem?.Dispose();
    _renderDebugSystem?.Dispose();
    _pointLightSystem?.Dispose();
  }
}
