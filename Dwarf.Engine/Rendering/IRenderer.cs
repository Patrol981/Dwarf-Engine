using Dwarf.AbstractionLayer;
using Dwarf.Math;
using Dwarf.Vulkan;
using Vortice.Vulkan;

public interface IRenderer : IDisposable {
  VkCommandBuffer BeginFrame(VkCommandBufferLevel level = VkCommandBufferLevel.Primary);
  void EndFrame();
  void BeginRendering(VkCommandBuffer commandBuffer);
  void EndRendering(VkCommandBuffer commandBuffer);
  void RecreateSwapchain();
  void CreateCommandBuffers(VkCommandPool commandPool, VkCommandBufferLevel level = VkCommandBufferLevel.Primary);

  VkCommandBuffer CurrentCommandBuffer { get; }
  int FrameIndex { get; }
  int ImageIndex { get; }
  float AspectRatio { get; }
  DwarfExtent2D Extent2D { get; }
  int MAX_FRAMES_IN_FLIGHT { get; }
  VulkanSwapchain Swapchain { get; }
  VulkanDynamicSwapchain DynamicSwapchain { get; }
  VkFormat DepthFormat { get; }
  CommandList CommandList { get; }

  VkRenderPass GetSwapchainRenderPass();
  VkRenderPass GetPostProcessingPass();

  void UpdateDescriptors();
  VkDescriptorSet PostProcessDecriptor { get; }
  VkDescriptorSet PreviousPostProcessDescriptor { get; }
}