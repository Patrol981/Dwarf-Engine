using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering.Particles;

public class ParticlePipelineProvider : PipelineProvider {
  public override unsafe VkVertexInputBindingDescription* GetBindingDescsFunc() {
    return null;
  }

  public override unsafe VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    return null;
  }

  public override uint GetAttribsLength() {
    return 0;
  }

  public override uint GetBindingsLength() {
    return 0;
  }
}