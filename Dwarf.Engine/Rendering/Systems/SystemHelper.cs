using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Engine;
public class SystemHelper {
  public static void CreatePipeline(ref VulkanDevice device, VkRenderPass renderPass, VkPipelineLayout layout, ref Pipeline pipeline, ref PipelineConfigInfo configInfo) {
    pipeline?.Dispose();
    if (configInfo != null) {
      configInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = configInfo!.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = layout;
    // pipeline = new Pipeline(device, "gui_vertex", "gui_fragment", pipelineConfig);
  }
}
