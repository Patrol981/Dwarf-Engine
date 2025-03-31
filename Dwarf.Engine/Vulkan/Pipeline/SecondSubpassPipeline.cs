using Vortice.Vulkan;

namespace Dwarf.Vulkan;

public class SecondSubpassPipeline : PipelineConfigInfo {
  public override PipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();

    configInfo.ColorBlendInfo.attachmentCount = 1;
    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.Back;
    configInfo.DepthStencilInfo.depthWriteEnable = false;
    configInfo.DepthStencilInfo.depthTestEnable = false;
    configInfo.Subpass = 1;
    return configInfo;
  }
}