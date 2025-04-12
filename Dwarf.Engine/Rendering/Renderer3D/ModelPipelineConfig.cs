using Dwarf.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public class ModelPipelineConfig : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();
    configInfo.Subpass = 0;
    return configInfo;
  }
}