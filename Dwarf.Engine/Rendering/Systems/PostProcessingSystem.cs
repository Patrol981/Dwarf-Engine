using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class PostProcessingSystem : SystemBase, IDisposable {
  public PostProcessingSystem(
    VmaAllocator vmaAllocator,
    IDevice device,
    Renderer renderer,
    Dictionary<string, DescriptorSetLayout> externalLayouts,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.AllGraphics)
      .AddBinding(1, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.AllGraphics)
      // .AddBinding(2, VkDescriptorType.SampledImage, VkShaderStageFlags.AllGraphics)
      // .AddBinding(3, VkDescriptorType.Sampler, VkShaderStageFlags.AllGraphics)
      .Build();

    VkDescriptorSetLayout[] layouts = [
      // renderer.Swapchain.InputAttachmentLayout.GetDescriptorSetLayout(),
      // externalLayouts["Global"].GetDescriptorSetLayout()
      _setLayout.GetDescriptorSetLayout()
    ];

    AddPipelineData(new() {
      RenderPass = renderer.GetPostProcessingPass(),
      VertexName = "post_process_index_vertex",
      FragmentName = "post_process_index_fragment",
      PipelineProvider = new SecondSubpassPipelineProvider(),
      DescriptorSetLayouts = layouts
    });

    Setup();
  }

  public void Setup() {
    _device.WaitQueue();

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(4)
      .AddPoolSize(VkDescriptorType.SampledImage, 10)
      .AddPoolSize(VkDescriptorType.Sampler, 10)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 10)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();
  }

  public void Render(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer);

    // VkDescriptorSet[] textures = [_renderer.Swapchain.]

    // vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, PipelineLayout, 0,)

    vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
  }

  public unsafe void Dispose() {
    base.Dispose();
  }
}