using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace DwarfEngine.Engine;
public class SystemHelper {
  public static void CreatePipeline(ref Device device, VkRenderPass renderPass, VkPipelineLayout layout, ref Pipeline pipeline, ref PipelineConfigInfo configInfo) {
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
