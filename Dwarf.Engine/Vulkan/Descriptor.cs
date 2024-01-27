using Dwarf.Engine;
using Dwarf.Utils;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;
public static class Descriptor {
  public static unsafe void BindDescriptorSet(
    VkDescriptorSet textureSet,
    FrameInfo frameInfo,
    ref VkPipelineLayout pipelineLayout,
    uint firstSet,
    uint setCount
  ) {
    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      firstSet,
      setCount,
      &textureSet,
      0,
      null
    );
  }

  public static unsafe void BindDescriptorSets(VkDescriptorSet[] descriptorSets, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout) {
    vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        pipelineLayout,
        0,
        descriptorSets
      );
  }
}