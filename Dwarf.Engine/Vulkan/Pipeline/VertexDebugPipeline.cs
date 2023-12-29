using Vortice.Vulkan;

namespace Dwarf.Vulkan;

public class VertexDebugPipeline : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();
    configInfo.RasterizationInfo.polygonMode = VkPolygonMode.Line;
    return configInfo;
  }
}