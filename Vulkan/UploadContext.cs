using Vortice.Vulkan;

namespace Dwarf.Vulkan;
public struct UploadContext {
  public VkFence UploadFence;
  public VkCommandPool CommandPool;
  public VkCommandBuffer CommandBuffer;
}
