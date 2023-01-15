using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public ref struct SwapChainSupportDetails {
  public VkSurfaceCapabilitiesKHR Capabilities;
  public ReadOnlySpan<VkSurfaceFormatKHR> Formats;
  public ReadOnlySpan<VkPresentModeKHR> PresentModes;
}


public static class Utils {
  public static unsafe void MemCopy(nint destination, nint source, int byteCount) =>
    Unsafe.CopyBlockUnaligned((void*)destination, (void*)source, (uint)byteCount);

  public static SwapChainSupportDetails QuerySwapChainSupport(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface) {
    SwapChainSupportDetails details = new SwapChainSupportDetails();
    vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, out details.Capabilities).CheckResult();

    details.Formats = vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface);
    details.PresentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface);
    return details;
  }
}