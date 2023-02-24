using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Vortice.Vulkan;
using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;
public unsafe static class DeviceHelper {
  public static VkPhysicalDevice GetPhysicalDevice(VkInstance instance, VkSurfaceKHR surface) {
    VkPhysicalDevice returnDevice = VkPhysicalDevice.Null;

    int count = 0;
    vkEnumeratePhysicalDevices(instance, &count, null).CheckResult();
    if (count == 0) {
      Logger.Error("Failed to find any Vulkan capable GPU");
    }

    vkEnumeratePhysicalDevices(instance, &count, null);

    VkPhysicalDevice[] physicalDevices = new VkPhysicalDevice[count];
    // VkPhysicalDevice* physicalDevices = stackalloc VkPhysicalDevice[count];
    fixed (VkPhysicalDevice* ptr = physicalDevices) {
      vkEnumeratePhysicalDevices(instance, &count, ptr);
    }

    VkPhysicalDeviceProperties gpuInfo = new();

    Logger.Info("Available GPU'S:");

    for (int i = 0; i < count; i++) {
      VkPhysicalDevice physicalDevice = physicalDevices[i];
      if (IsDeviceSuitable(physicalDevice, surface) == false)
        continue;

      vkGetPhysicalDeviceProperties(physicalDevice, out var checkProperties);
      Logger.Info($"{checkProperties.GetDeviceName().ToString()}");
      bool discrete = checkProperties.deviceType == VkPhysicalDeviceType.DiscreteGpu;
      if (discrete || returnDevice.IsNull) {
        gpuInfo = checkProperties;
        returnDevice = physicalDevice;
        if (discrete) break;
      }
    }

    Logger.Info($"Successfully found a device: {gpuInfo.GetDeviceName().ToString()}");

    return returnDevice;
  }
  public static bool IsSupported() {
    try {
      VkResult result = vkInitialize();
      if (result != VkResult.Success)
        return false;
      VkVersion version = vkEnumerateInstanceVersion();
      if (version < VkVersion.Version_1_1)
        return false;
      return true;
    } catch {
      return false;
    }
  }

  public static string[] EnumerateInstanceLayers() {
    if (!IsSupported()) {
      return Array.Empty<string>();
    }

    int count = 0;
    VkResult result = vkEnumerateInstanceLayerProperties(&count, null);
    if (result != VkResult.Success) {
      return Array.Empty<string>();
    }

    if (count == 0) {
      return Array.Empty<string>();
    }

    VkLayerProperties[] properties = new VkLayerProperties[count];
    // VkLayerProperties* properties = stackalloc VkLayerProperties[count];

    fixed (VkLayerProperties* ptr = properties) {
      vkEnumerateInstanceLayerProperties(&count, ptr).CheckResult();
    }

    string[] resultExt = new string[count];
    for (int i = 0; i < count; i++) {
      resultExt[i] = properties[i].GetLayerName();
    }

    return resultExt;
  }

  public static void GetOptimalValidationLayers(HashSet<string> availableLayers, List<string> instanceLayers) {
    // The preferred validation layer is "VK_LAYER_KHRONOS_validation"
    List<string> validationLayers = new()
    {
            "VK_LAYER_KHRONOS_validation"
        };

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }

    // Otherwise we fallback to using the LunarG meta layer
    validationLayers = new()
    {
            "VK_LAYER_LUNARG_standard_validation"
        };

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }

    // Otherwise we attempt to enable the individual layers that compose the LunarG meta layer since it doesn't exist
    validationLayers = new()
    {
            "VK_LAYER_GOOGLE_threading",
            "VK_LAYER_LUNARG_parameter_validation",
            "VK_LAYER_LUNARG_object_tracker",
            "VK_LAYER_LUNARG_core_validation",
            "VK_LAYER_GOOGLE_unique_objects",
        };

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }

    // Otherwise as a last resort we fallback to attempting to enable the LunarG core layer
    validationLayers = new()
    {
            "VK_LAYER_LUNARG_core_validation"
        };

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }
  }

  private static bool ValidateLayers(List<string> required, HashSet<string> availableLayers) {
    foreach (string layer in required) {
      bool found = false;
      foreach (string availableLayer in availableLayers) {
        if (availableLayer == layer) {
          found = true;
          break;
        }
      }

      if (!found) {
        //Log.Warn("Validation Layer '{}' not found", layer);
        return false;
      }
    }

    return true;
  }

  private static bool IsDeviceSuitable(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface) {
    var checkQueueFamilies = FindQueueFamilies(physicalDevice, surface);
    if (checkQueueFamilies.graphicsFamily == VK_QUEUE_FAMILY_IGNORED)
      return false;

    if (checkQueueFamilies.presentFamily == VK_QUEUE_FAMILY_IGNORED)
      return false;

    SwapChainSupportDetails swapChainSupport = Utils.QuerySwapChainSupport(physicalDevice, surface);
    return !swapChainSupport.Formats.IsEmpty && !swapChainSupport.PresentModes.IsEmpty;
  }

  public static string[] GetInstanceExtensions() {
    int count = 0;
    VkResult result = vkEnumerateInstanceExtensionProperties((byte*)null, &count, null);
    if (result != VkResult.Success) {
      return Array.Empty<string>();
    }

    if (count == 0) {
      return Array.Empty<string>();
    }

    VkExtensionProperties[] props = new VkExtensionProperties[count];
    // VkExtensionProperties* props = stackalloc VkExtensionProperties[count];
    fixed (VkExtensionProperties* ptr = props) {
      vkEnumerateInstanceExtensionProperties((byte*)null, &count, ptr);
    }

    string[] extensions = new string[count];
    for (int i = 0; i < count; i++) {
      extensions[i] = props[i].GetExtensionName();
    }

    return extensions;
  }

  public static (uint graphicsFamily, uint presentFamily) FindQueueFamilies(
      VkPhysicalDevice device, VkSurfaceKHR surface) {
    ReadOnlySpan<VkQueueFamilyProperties> queueFamilies = vkGetPhysicalDeviceQueueFamilyProperties(device);

    uint graphicsFamily = VK_QUEUE_FAMILY_IGNORED;
    uint presentFamily = VK_QUEUE_FAMILY_IGNORED;
    uint i = 0;
    foreach (VkQueueFamilyProperties queueFamily in queueFamilies) {
      if ((queueFamily.queueFlags & VkQueueFlags.Graphics) != VkQueueFlags.None) {
        graphicsFamily = i;
      }

      vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, out VkBool32 presentSupport);
      if (presentSupport) {
        presentFamily = i;
      }

      if (graphicsFamily != VK_QUEUE_FAMILY_IGNORED
          && presentFamily != VK_QUEUE_FAMILY_IGNORED) {
        break;
      }

      i++;
    }

    return (graphicsFamily, presentFamily);
  }
}