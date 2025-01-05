using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class SubpassConnectorSystem : SystemBase, IDisposable {
  public const string Subpass = "Subpass";
  public SubpassConnectorSystem(
    VmaAllocator vmaAllocator,
    IDevice device,
    Renderer renderer,
    Dictionary<string, DescriptorSetLayout> externalLayouts,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    VkDescriptorSetLayout[] layouts = [renderer.Swapchain.InputAttachmentLayout.GetDescriptorSetLayout()];

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "subpass_read_vertex",
      FragmentName = "subpass_read_fragment",
      PipelineProvider = new SecondSubpassPipelineProvider(),
      DescriptorSetLayouts = layouts,
      PipelineName = Subpass
    });
  }

  private unsafe void UpdateDescriptors(int currentFrame) {
    // for (int i = 0; i < _renderer.MAX_FRAMES_IN_FLIGHT; i++) {
    //   _renderer.Swapchain.UpdateDescriptors(i);
    // }
    _renderer.Swapchain.UpdateDescriptors(currentFrame);
  }

  public void Redner(FrameInfo frameInfo) {
    UpdateDescriptors(_renderer.GetFrameIndex());
    BindPipeline(frameInfo.CommandBuffer, Subpass);
    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      _pipelines[Subpass].PipelineLayout,
      0,
      _renderer.Swapchain.ImageDescriptor
    );
    vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
  }

  public void Dispose() {
    base.Dispose();
  }
}