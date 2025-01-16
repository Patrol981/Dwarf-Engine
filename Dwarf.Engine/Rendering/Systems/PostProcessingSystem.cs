using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public struct PostProcessInfo {
  public Vector2 WindowSize;
  public float DepthMin;
  public float DepthMax;
  public float EdgeLow;
  public float EdgeHigh;
  public float Contrast;
  public float Stripple;
}

public class PostProcessingSystem : SystemBase, IDisposable {
  public static float DepthMax = 0.995f;
  public static float DepthMin = 0.990f;
  public static float EdgeLow = 0;
  public static float EdgeHigh = 1;
  public static float Contrast = 0;
  public static float Stipple = 0;

  private readonly unsafe PostProcessInfo* _postProcessInfoPushConstant =
    (PostProcessInfo*)Marshal.AllocHGlobal(Unsafe.SizeOf<PostProcessInfo>());

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
      _setLayout.GetDescriptorSetLayout(),
      externalLayouts["Global"].GetDescriptorSetLayout()
    ];

    AddPipelineData<PostProcessInfo>(new() {
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

  private void UpdateDescriptors(int currentFrame) {
    _renderer.Swapchain.UpdatePostProcessDescriptors(currentFrame);
  }

  public void Render(FrameInfo frameInfo) {
    UpdateDescriptors(_renderer.GetFrameIndex());
    BindPipeline(frameInfo.CommandBuffer);

    var window = Application.Instance.Window;
    unsafe {
      _postProcessInfoPushConstant->DepthMax = DepthMax;
      _postProcessInfoPushConstant->DepthMin = DepthMin;
      _postProcessInfoPushConstant->WindowSize = new(window.Extent.Width, window.Extent.Height);
      _postProcessInfoPushConstant->EdgeLow = EdgeLow;
      _postProcessInfoPushConstant->EdgeHigh = EdgeHigh;
      _postProcessInfoPushConstant->Contrast = Contrast;
      _postProcessInfoPushConstant->Stripple = Stipple;

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<PostProcessInfo>(),
        _postProcessInfoPushConstant
      );
    }

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      0,
      _renderer.Swapchain.PostProcessDecriptor
    );

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      1,
      frameInfo.GlobalDescriptorSet
    );

    vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
  }

  public unsafe void Dispose() {
    base.Dispose();
  }
}