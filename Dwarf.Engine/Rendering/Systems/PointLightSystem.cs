using Dwarf.AbstractionLayer;
using Dwarf.Rendering.Lightning;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Systems;

public class PointLightSystem : SystemBase {
  public PointLightSystem(
    IDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(
      renderer.GetSwapchainRenderPass(),
      "point_light_vertex",
      "point_light_fragment",
      new PipelinePointLightProvider()
   );
  }

  public void Setup() {
    _device.WaitQueue();
  }

  public void Render(FrameInfo frameInfo) {
    // if (lights.Length < 1) return;

    _pipeline.Bind(frameInfo.CommandBuffer);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelineLayout,
        0,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );
    }

    vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);

    // Logger.Info("Render");
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    base.Dispose();
  }
}