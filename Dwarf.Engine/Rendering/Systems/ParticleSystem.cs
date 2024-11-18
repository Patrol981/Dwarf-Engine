using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Vulkan;

namespace Dwarf.Rendering;

public class ParticleSystem : SystemBase, IRenderSystem {
  public ParticleSystem(
    IDevice device,
    Renderer renderer,
    PipelineConfigInfo configInfo = null
  ) : base(device, renderer, configInfo) {

  }

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    throw new NotImplementedException();
  }
}