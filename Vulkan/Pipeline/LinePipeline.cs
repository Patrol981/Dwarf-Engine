using Vortice.Vulkan;

namespace Dwarf.Vulkan;
public class LinePipeline : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();
    configInfo.InputAssemblyInfo.topology = VkPrimitiveTopology.LineList;
    return configInfo;
  }
}
