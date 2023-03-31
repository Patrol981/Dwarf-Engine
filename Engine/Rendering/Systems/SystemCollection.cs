﻿using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Engine.Rendering;
public class SystemCollection : IDisposable {
  private Render3DSystem? _render3DSystem;
  private Render2DSystem? _render2DSystem;
  private RenderUISystem? _renderUISystem;

  public bool Reload3DRenderSystem = false;
  public bool Reload2DRenderSystem = false;
  public bool ReloadUISystem = false;

  public void UpdateSystems(ReadOnlySpan<Entity> entities, FrameInfo frameInfo) {
    _render3DSystem?.RenderEntities(frameInfo, Entity.Distinct<Model>(entities).ToArray());
    _render2DSystem?.RenderEntities(frameInfo, Entity.Distinct<Sprite>(entities).ToArray());
    _renderUISystem?.DrawUI();
  }

  public void ValidateSystems(
    ReadOnlySpan<Entity> entities,
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    PipelineConfigInfo pipelineConfigInfo,
    ref TextureManager textureManager
  ) {
    if (_render3DSystem != null) {
      var modelEntities = Entity.Distinct<Model>(entities).ToArray();
      var sizes = _render3DSystem.CheckSizes(modelEntities);
      var textures = _render3DSystem.CheckTextures(modelEntities);
      if (!sizes || !textures || Reload3DRenderSystem) {
        Reload3DRenderSystem = false;
        Reload3DRenderer(device, renderer, globalLayout, ref textureManager, pipelineConfigInfo, entities);
      }
    }

    if (_render2DSystem != null) {
      var spriteEntities = Entity.Distinct<Sprite>(entities).ToArray();
      var sizes = _render2DSystem.CheckSizes(spriteEntities);
      var textures = _render2DSystem.CheckTextures(spriteEntities);
      if (!sizes || !textures || Reload2DRenderSystem) {
        Reload2DRenderSystem = false;
        Reload2DRenderer(device, renderer, globalLayout, ref textureManager, pipelineConfigInfo, entities);
      }
    }
  }

  public void ReloadSystems(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    PipelineConfigInfo pipelineConfigInfo,
    ReadOnlySpan<Entity> entities,
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
      _renderUISystem = new RenderUISystem(device, renderer.GetSwapchainRenderPass());
    }

    SetupRenderDatas(entities, ref textureManager, renderer);
  }

  public void SetupRenderDatas(ReadOnlySpan<Entity> entities, ref TextureManager textureManager, Renderer renderer) {
    if (_render3DSystem != null) {
      _render3DSystem.SetupRenderData(Entity.Distinct<Model>(entities).ToArray(), ref textureManager);
    }

    if (_render2DSystem != null) {
      _render2DSystem.Setup(Entity.Distinct<Sprite>(entities).ToArray(), ref textureManager);
    }

    if (_renderUISystem != null) {
      _renderUISystem.SetupUIData(1000, (int)renderer.Extent2D.width, (int)renderer.Extent2D.height);
    }
  }

  public void Reload3DRenderer(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig,
    ReadOnlySpan<Entity> entities
  ) {
    _render3DSystem?.Dispose();
    SetRender3DSystem((Render3DSystem)new Render3DSystem().Create(
      device,
      renderer,
      globalLayout,
      pipelineConfig
    ));
    _render3DSystem?.SetupRenderData(Entity.Distinct<Model>(entities).ToArray(), ref textureManager);
  }

  public void Reload2DRenderer(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig,
    ReadOnlySpan<Entity> entities
  ) {
    _render2DSystem?.Dispose();
    SetRender2DSystem((Render2DSystem)new Render2DSystem().Create(
      device,
      renderer,
      globalLayout,
      pipelineConfig
    ));
    _render2DSystem?.Setup(Entity.Distinct<Sprite>(entities).ToArray(), ref textureManager);
  }

  public void SetRender3DSystem(Render3DSystem render3DSystem) {
    _render3DSystem = render3DSystem;
  }

  public void SetRender2DSystem(Render2DSystem render2DSystem) {
    _render2DSystem = render2DSystem;
  }

  public void SetRenderUISystem(RenderUISystem renderUISystem) {
    _renderUISystem = renderUISystem;
  }

  public Render3DSystem GetRender3DSystem() {
    return _render3DSystem ?? null!;
  }

  public Render2DSystem GetRender2DSystem() {
    return _render2DSystem ?? null!;
  }

  public RenderUISystem GetRenderUISystem() {
    return _renderUISystem ?? null!;
  }

  public void Dispose() {
    _render3DSystem?.Dispose();
    _render2DSystem?.Dispose();
    _renderUISystem?.Dispose();
  }
}
