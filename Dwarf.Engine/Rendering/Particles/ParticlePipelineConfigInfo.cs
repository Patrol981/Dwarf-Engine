using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering.Particles;

public class ParticlePipelineConfigInfo : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();

    configInfo.DepthStencilInfo.depthWriteEnable = true;
    configInfo.DepthStencilInfo.depthCompareOp = VkCompareOp.GreaterOrEqual;

    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.Back;

    configInfo.ColorBlendInfo.logicOp = VkLogicOp.Clear;

    configInfo.Subpass = 1;

    return configInfo;
  }
}