using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public class VertexDebugPipeline : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();
    configInfo.RasterizationInfo.polygonMode = VkPolygonMode.Line;
    configInfo.RasterizationInfo.lineWidth = 1.0f;

    configInfo.Subpass = 0;
    return configInfo;
  }
}