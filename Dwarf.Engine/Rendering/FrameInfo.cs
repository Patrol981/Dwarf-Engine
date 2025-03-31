using Dwarf.EntityComponentSystem;
using Vortice.Vulkan;

namespace Dwarf;
public struct FrameInfo {
  public int FrameIndex;
  public VkCommandBuffer CommandBuffer;
  public Camera Camera;
  public VkDescriptorSet GlobalDescriptorSet;
  public VkDescriptorSet PointLightsDescriptorSet;
  public VkDescriptorSet ObjectDataDescriptorSet;
  public VkDescriptorSet JointsBufferDescriptorSet;
  public TextureManager TextureManager;
  public Entity ImportantEntity;
}