using Dwarf.Extensions.Logging;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanSwapchain2 : IDisposable {
  private const int MAX_FRAMES_IN_FLIGHT = 2;

  private readonly VulkanDevice Device;

  public void InitSurface() {

  }

  public void SetContext() {

  }

  public void Create() {

  }

  public VkResult PresentQueue() {
    return VkResult.Success;
  }

  public VkResult AcquireNextImage() {
    return VkResult.Success;
  }

  public void Dispose() {

  }
}