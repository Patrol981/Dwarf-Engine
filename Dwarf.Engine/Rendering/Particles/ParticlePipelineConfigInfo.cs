using Dwarf.Vulkan;

namespace Dwarf.Rendering.Particles;

public class ParticlePipelineConfigInfo : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();

    configInfo.Subpass = 1;

    return configInfo;
  }
}