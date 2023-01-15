using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Vortice.Vulkan;
using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;
public unsafe static class DeviceHelper {
  public static VkDevice CreateLogicalDevice(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface) {
    var queueFamilies = FindQueueFamilies(physicalDevice, surface);

    var queueCreateInfo = new VkDeviceQueueCreateInfo {
      sType = VkStructureType.DeviceQueueCreateInfo,
      queueFamilyIndex = queueFamilies.graphicsFamily,
      queueCount = 1,
    };

    float priority = 1.0f;
    queueCreateInfo.pQueuePriorities = &priority;

    List<string> enabledExtensions = new() {
      VK_KHR_SWAPCHAIN_EXTENSION_NAME
    };

    VkPhysicalDeviceFeatures2 deviceFeatures2 = new() {
      sType = VkStructureType.PhysicalDeviceFeatures2
    };
    using var deviceExtensionNames = new VkStringArray(enabledExtensions);

    var physicalDeviceFeatures = new VkPhysicalDeviceFeatures {

    };

    var createInfo = new VkDeviceCreateInfo {
      sType = VkStructureType.DeviceCreateInfo,
      pNext = default,
      queueCreateInfoCount = 1,
      pQueueCreateInfos = &queueCreateInfo,
      enabledExtensionCount = deviceExtensionNames.Length,
      ppEnabledExtensionNames = deviceExtensionNames,
      pEnabledFeatures = null,
    };

    VkDevice device = VkDevice.Null;
    vkCreateDevice(physicalDevice, &createInfo, null, out device);
    return device;
  }
  public static VkPhysicalDevice GetPhysicalDevice(VkInstance instance, VkSurfaceKHR surface) {
    VkPhysicalDevice returnDevice = VkPhysicalDevice.Null;

    int count = 0;
    vkEnumeratePhysicalDevices(instance, &count, null).CheckResult();
    if (count == 0) {
      Logger.Error("Faild to find any Vulkan capable GPU");
    }

    vkEnumeratePhysicalDevices(instance, &count, null);
    VkPhysicalDevice* physicalDevices = stackalloc VkPhysicalDevice[count];
    vkEnumeratePhysicalDevices(instance, &count, physicalDevices);

    for (int i = 0; i < count; i++) {
      VkPhysicalDevice physicalDevice = physicalDevices[i];
      if (IsDeviceSuitable(physicalDevice, surface) == false)
        continue;

      vkGetPhysicalDeviceProperties(physicalDevice, out VkPhysicalDeviceProperties checkProperties);
      bool discrete = checkProperties.deviceType == VkPhysicalDeviceType.DiscreteGpu;
      if (discrete || returnDevice.IsNull) {
        returnDevice = physicalDevice;
        if (discrete) break;
      }
    }

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

  public static List<string> GetInstanceLayers() {
    // get available
    HashSet<string> availableLayers = new(EnumerateInstanceLayers());
    List<string> instanceLayers = new();

    // validate
    // GetOptimalValidationLayers(availableLayers, instanceLayers);

    return instanceLayers;
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

    VkLayerProperties* properties = stackalloc VkLayerProperties[count];
    vkEnumerateInstanceLayerProperties(&count, properties).CheckResult();

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

    VkExtensionProperties* props = stackalloc VkExtensionProperties[count];
    vkEnumerateInstanceExtensionProperties((byte*)null, &count, props);

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