using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

  public static IntPtr ToIntPtr<T>(T[] arr) where T : struct {
    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;
    try {
      ptr = Marshal.AllocHGlobal(size * arr.Length);
      for (int i = 0; i < arr.Length; i++) {
        Marshal.StructureToPtr(arr[i], IntPtr.Add(ptr, i * size), true);
      }
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }

  public static IntPtr ToIntPtr<T>(T data) where T : struct {
    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;
    try {
      ptr = Marshal.AllocHGlobal(size);
      Marshal.StructureToPtr(data, ptr, true);
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }
}