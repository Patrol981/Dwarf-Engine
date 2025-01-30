using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Dwarf.Windowing;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public struct SubpassInfo {
  public Vector2 WindowSize;
  public float DepthMin;
  public float DepthMax;
  public float EdgeLow;
  public float EdgeHigh;
  public float Contrast;
  public float Stripple;
}

public class SubpassConnectorSystem : SystemBase, IDisposable {
  public const string Subpass = "Subpass";

  private readonly unsafe SubpassInfo* _subpassInfoPushConstant =
    (SubpassInfo*)Marshal.AllocHGlobal(Unsafe.SizeOf<SubpassInfo>());

  public SubpassConnectorSystem(
    VmaAllocator vmaAllocator,
    IDevice device,
    Renderer renderer,
    Dictionary<string, DescriptorSetLayout> externalLayouts,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    VkDescriptorSetLayout[] layouts = [
      renderer.Swapchain.InputAttachmentLayout.GetDescriptorSetLayout(),
      externalLayouts["Global"].GetDescriptorSetLayout()
    ];

    AddPipelineData<SubpassInfo>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "subpass_read_vertex",
      FragmentName = "subpass_read_fragment",
      PipelineProvider = new SecondSubpassPipelineProvider(),
      DescriptorSetLayouts = layouts,
      PipelineName = Subpass
    });
  }

  private unsafe void UpdateDescriptors(int currentFrame) {
    _renderer.Swapchain.UpdateDescriptors(currentFrame);
    // _renderer.Swapchain.UpdatePostProcessDescriptors(currentFrame);
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

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      _pipelines[Subpass].PipelineLayout,
      1,
      frameInfo.GlobalDescriptorSet
    );
  }

  public unsafe void Dispose() {
    MemoryUtils.FreeIntPtr<SubpassInfo>((nint)_subpassInfoPushConstant);

    base.Dispose();
  }
}