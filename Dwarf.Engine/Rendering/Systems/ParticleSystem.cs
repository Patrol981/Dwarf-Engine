using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering;

public class ParticleSystem : SystemBase, IRenderSystem {
  public ParticleSystem(
    VmaAllocator vmaAllocator,
    IDevice device,
    Renderer renderer,
    PipelineConfigInfo configInfo = null
  ) : base(vmaAllocator, device, renderer, configInfo) {

  }

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    throw new NotImplementedException();
  }
}