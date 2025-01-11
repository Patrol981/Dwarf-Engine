namespace Dwarf.Vulkan;

public class ModelPipelineConfig : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();
    configInfo.Subpass = 0;
    return configInfo;
  }
}