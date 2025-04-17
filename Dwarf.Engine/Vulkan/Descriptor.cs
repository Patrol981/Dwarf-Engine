using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;
public static class Descriptor {
  public static unsafe void BindDescriptorSet(
    VkDescriptorSet descriptorSet,
    FrameInfo frameInfo,
    VkPipelineLayout pipelineLayout,
    uint firstSet,
    uint setCount
  ) {
    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      firstSet,
      setCount,
      &descriptorSet,
      0,
      null
    );
  }

  public static unsafe void BindDescriptorSet(
    VkDescriptorSet descriptorSet,
    nint commandBuffer,
    VkPipelineLayout pipelineLayout,
    uint firstSet,
    uint setCount
  ) {
    vkCmdBindDescriptorSets(
      commandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      firstSet,
      setCount,
      &descriptorSet,
      0,
      null
    );
  }

  public static unsafe void BindDescriptorSets(
    VkDescriptorSet[] descriptorSets,
    FrameInfo frameInfo,
    VkPipelineLayout pipelineLayout,
    uint firstSet
  ) {
    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      firstSet,
      descriptorSets
    );
  }
}