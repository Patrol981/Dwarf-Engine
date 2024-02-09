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
    Application.Instance.Device._mutex.WaitOne();
    vkDeviceWaitIdle(Application.Instance.Device.LogicalDevice);
    vkQueueWaitIdle(Application.Instance.Device.GraphicsQueue);
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
    Application.Instance.Device._mutex.ReleaseMutex();
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