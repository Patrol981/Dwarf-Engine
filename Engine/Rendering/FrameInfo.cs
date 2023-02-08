using Vortice.Vulkan;

namespace Dwarf.Engine;
public struct FrameInfo {
  public int FrameIndex;
  public VkCommandBuffer CommandBuffer;
  public Camera Camera;
}