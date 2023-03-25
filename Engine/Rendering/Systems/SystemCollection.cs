using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Engine.Rendering;
public class SystemCollection : IDisposable {
  private Render3DSystem? _render3DSystem;
  private RenderUISystem? _renderUISystem;

  public void UpdateSystems(ReadOnlySpan<Entity> entities, FrameInfo frameInfo) {
    _render3DSystem?.RenderEntities(frameInfo, Entity.Distinct<Model>(entities).ToArray());
    _renderUISystem?.DrawUI();
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

    if (_renderUISystem != null) {
      _renderUISystem.Dispose();
      _renderUISystem = new RenderUISystem(device, renderer.GetSwapchainRenderPass());
    }

    SetupRenderDatas(entities, ref textureManager, renderer);
  }

  public void SetupRenderDatas(ReadOnlySpan<Entity> entities, ref TextureManager textureManager, Renderer renderer) {
    if (_render3DSystem != null) {
      _render3DSystem.SetupRenderData(entities, ref textureManager);
    }

    if (_renderUISystem != null) {
      _renderUISystem.SetupUIData(1000, (int)renderer.Extent2D.width, (int)renderer.Extent2D.height);
    }
  }

  public void SetRender3DSystem(Render3DSystem render3DSystem) {
    _render3DSystem = render3DSystem;
  }

  public void SetRenderUISystem(RenderUISystem renderUISystem) {
    _renderUISystem = renderUISystem;
  }

  public Render3DSystem GetRender3DSystem() {
    return _render3DSystem ?? null!;
  }

  public RenderUISystem GetRenderUISystem() {
    return _renderUISystem ?? null!;
  }

  public void Dispose() {
    _render3DSystem?.Dispose();
    _renderUISystem?.Dispose();
  }
}
