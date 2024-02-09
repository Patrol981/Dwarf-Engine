using Vortice.Vulkan;

namespace Dwarf.Rendering;
public struct ThreadInfo {
  public VkCommandPool CommandPool;
  public VkCommandBuffer[] CommandBuffer;
}
