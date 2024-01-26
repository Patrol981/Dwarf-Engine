using System.Diagnostics;
using System.Runtime.InteropServices;

using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;

using Vortice.Vulkan;

using static Dwarf.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class Device : IDisposable {
  private readonly string[] VALIDATION_LAYERS = { "VK_LAYER_KHRONOS_validation" };
  public static bool s_EnableValidationLayers = true;
  private readonly Window _window;

  private VkDebugUtilsMessengerEXT _debugMessenger = VkDebugUtilsMessengerEXT.Null;

  private VkInstance _vkInstance = VkInstance.Null;
  private VkSurfaceKHR _surface = VkSurfaceKHR.Null;
  private VkPhysicalDevice _physicalDevice = VkPhysicalDevice.Null;
  private VkDevice _logicalDevice = VkDevice.Null;

  private VkCommandPool _commandPool = VkCommandPool.Null;
  private readonly object _commandPoolLock = new object();

  public VkQueue GraphicsQueue = VkQueue.Null;
  public VkQueue PresentQueue = VkQueue.Null;
  public VkPhysicalDeviceProperties Properties;

  internal Mutex _mutex = new();

  public Device(Window window) {
    _window = window;
    CreateInstance();
    CreateSurface();
    PickPhysicalDevice();
    CreateLogicalDevice();
    CreateCommandPool();
  }

  public unsafe void CreateBuffer(
    ulong size,
    VkBufferUsageFlags uFlags,
    VkMemoryPropertyFlags pFlags,
    out VkBuffer buffer,
    out VkDeviceMemory bufferMemory
  ) {
    VkBufferCreateInfo bufferInfo = new() {
      size = size,
      usage = uFlags,
      sharingMode = VkSharingMode.Exclusive
    };

    vkCreateBuffer(_logicalDevice, &bufferInfo, null, out buffer).CheckResult();

    VkMemoryRequirements memRequirements;
    vkGetBufferMemoryRequirements(_logicalDevice, buffer, out memRequirements);

    VkMemoryAllocateInfo allocInfo = new() {
      allocationSize = memRequirements.size,
      memoryTypeIndex = FindMemoryType(memRequirements.memoryTypeBits, pFlags)
    };

    vkAllocateMemory(_logicalDevice, &allocInfo, null, out bufferMemory).CheckResult();
    vkBindBufferMemory(_logicalDevice, buffer, bufferMemory, 0).CheckResult();
  }

  public unsafe Task CopyBuffer(VkBuffer srcBuffer, VkBuffer dstBuffer, ulong size) {
    VkCommandBuffer commandBuffer = BeginSingleTimeCommands();

    VkBufferCopy copyRegion = new() {
      srcOffset = 0,  // Optional
      dstOffset = 0,  // Optional
      size = size
    };
    vkCmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, &copyRegion);

    EndSingleTimeCommands(commandBuffer);

    return Task.CompletedTask;
  }

  public static unsafe VkSemaphore CreateSemaphore(Device device) {
    var semaphoreInfo = new VkSemaphoreCreateInfo();
    var semaphore = new VkSemaphore();
    vkCreateSemaphore(device.LogicalDevice, &semaphoreInfo, null, &semaphore);
    return semaphore;
  }

  public static unsafe void DestroySemaphore(Device device, VkSemaphore semaphore) {
    vkDestroySemaphore(device.LogicalDevice, semaphore, null);
  }

  public VkFormat FindSupportedFormat(List<VkFormat> candidates, VkImageTiling tilling, VkFormatFeatureFlags features) {
    foreach (var format in candidates) {
      VkFormatProperties props;
      vkGetPhysicalDeviceFormatProperties(_physicalDevice, format, out props);

      if (tilling == VkImageTiling.Linear && (props.linearTilingFeatures & features) == features) {
        return format;
      } else if (tilling == VkImageTiling.Optimal && (props.optimalTilingFeatures & features) == features) {
        return format;
      }
    }
    throw new Exception("failed to find candidate!");
  }

  public unsafe void CreateImageWithInfo(
    VkImageCreateInfo imageInfo,
    VkMemoryPropertyFlags properties,
    out VkImage image,
    out VkDeviceMemory imageMemory
  ) {

    vkCreateImage(_logicalDevice, &imageInfo, null, out image).CheckResult();

    VkMemoryRequirements memRequirements;
    vkGetImageMemoryRequirements(_logicalDevice, image, out memRequirements);

    VkMemoryAllocateInfo allocInfo = new() {
      allocationSize = memRequirements.size,
      memoryTypeIndex = FindMemoryType(memRequirements.memoryTypeBits, properties)
    };

    vkAllocateMemory(_logicalDevice, &allocInfo, null, out imageMemory).CheckResult();
    vkBindImageMemory(_logicalDevice, image, imageMemory, 0);
  }

  public uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties) {
    VkPhysicalDeviceMemoryProperties memProperties;
    vkGetPhysicalDeviceMemoryProperties(_physicalDevice, out memProperties);
    for (int i = 0; i < memProperties.memoryTypeCount; i++) {
      // 1 << n is basically an equivalent to 2^n.
      // if ((typeFilter & (1 << i)) &&

      if ((memProperties.memoryTypes[i].propertyFlags & properties) == properties) {
        return (uint)i;
      }

      //if ((typeFilter & (1 << 1)) != 0 && (memProperties.memoryTypes[i].propertyFlags & properties) == properties) {
      //  return (uint)i;
      //}
    }

    throw new Exception($"Failed to find suitable memory type");
  }

  public unsafe VkCommandBuffer BeginSingleTimeCommands() {
    VkCommandBufferAllocateInfo allocInfo = new() {
      level = VkCommandBufferLevel.Primary,
      commandPool = _commandPool,
      commandBufferCount = 1
    };

    VkCommandBuffer commandBuffer;
    vkAllocateCommandBuffers(_logicalDevice, &allocInfo, &commandBuffer);

    VkCommandBufferBeginInfo beginInfo = new() {
      flags = VkCommandBufferUsageFlags.OneTimeSubmit
    };

    vkBeginCommandBuffer(commandBuffer, &beginInfo);
    return commandBuffer;
  }

  public unsafe void EndSingleTimeCommands(VkCommandBuffer commandBuffer) {
    vkEndCommandBuffer(commandBuffer);

    VkSubmitInfo submitInfo = new() {
      commandBufferCount = 1,
      pCommandBuffers = &commandBuffer,
    };

    vkQueueWaitIdle(GraphicsQueue);
    vkQueueSubmit(GraphicsQueue, 1, &submitInfo, VkFence.Null);
    vkQueueWaitIdle(GraphicsQueue);

    vkFreeCommandBuffers(_logicalDevice, _commandPool, 1, &commandBuffer);
  }

  private unsafe void CreateInstance() {
    HashSet<string> availableInstanceLayers = new(DeviceHelper.EnumerateInstanceLayers());
    HashSet<string> availableInstanceExtensions = new(DeviceHelper.GetInstanceExtensions());

    var appInfo = new VkApplicationInfo {
      pApplicationName = new VkString("Dwarf App"),
      applicationVersion = new(1, 0, 0),
      pEngineName = new VkString("Dwarf Engine"),
      engineVersion = new(1, 0, 0),
      apiVersion = VkVersion.Version_1_3
    };

    var createInfo = new VkInstanceCreateInfo {
      pApplicationInfo = &appInfo
    };

    List<string> instanceExtensions = [.. glfwGetRequiredInstanceExtensions()];

    List<string> instanceLayers = new();
    // Check if VK_EXT_debug_utils is supported, which supersedes VK_EXT_Debug_Report
    foreach (string availableExtension in availableInstanceExtensions) {
      if (availableExtension == VK_EXT_DEBUG_UTILS_EXTENSION_NAME) {
        instanceExtensions.Add(VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
      } else if (availableExtension == VK_EXT_SWAPCHAIN_COLOR_SPACE_EXTENSION_NAME) {
        instanceExtensions.Add(VK_EXT_SWAPCHAIN_COLOR_SPACE_EXTENSION_NAME);
      }
    }
    // instanceExtensions.Add(VK_EXT_PIPELINE_CREATION_CACHE_CONTROL_EXTENSION_NAME);

    if (s_EnableValidationLayers) {
      DeviceHelper.GetOptimalValidationLayers(availableInstanceLayers, instanceLayers);
    }

    using VkStringArray vkLayerNames = new(instanceLayers);
    using VkStringArray vkInstanceExtensions = new(instanceExtensions);

    createInfo.enabledLayerCount = vkLayerNames.Length;
    createInfo.ppEnabledLayerNames = vkLayerNames;
    createInfo.enabledExtensionCount = vkInstanceExtensions.Length;
    createInfo.ppEnabledExtensionNames = vkInstanceExtensions;

    var debugCreateInfo = new VkDebugUtilsMessengerCreateInfoEXT();

    if (instanceLayers.Count > 0 && s_EnableValidationLayers) {
      debugCreateInfo = SetupDebugCallbacks();
      createInfo.pNext = &debugCreateInfo;
    } else {
      createInfo.pNext = null;
    }

    var result = vkCreateInstance(&createInfo, null, out _vkInstance);
    if (result != VkResult.Success) throw new Exception("Failed to create instance!");

    vkLoadInstanceOnly(_vkInstance);

    if (instanceLayers.Count > 0) {
      vkCreateDebugUtilsMessengerEXT(_vkInstance, &debugCreateInfo, null, out _debugMessenger).CheckResult();
    }
  }

  private unsafe VkDebugUtilsMessengerCreateInfoEXT SetupDebugCallbacks() {
    Logger.Info("Creating Debug Callbacks...");
    var createInfo = new VkDebugUtilsMessengerCreateInfoEXT {
      messageSeverity =
        VkDebugUtilsMessageSeverityFlagsEXT.Error |
        VkDebugUtilsMessageSeverityFlagsEXT.Warning,
      messageType =
        VkDebugUtilsMessageTypeFlagsEXT.General |
        VkDebugUtilsMessageTypeFlagsEXT.Validation |
        VkDebugUtilsMessageTypeFlagsEXT.Performance,
      pfnUserCallback = &DebugMessengerCallback,
      pUserData = null
    };

    return createInfo;
  }

  [UnmanagedCallersOnly]
  private unsafe static uint DebugMessengerCallback(VkDebugUtilsMessageSeverityFlagsEXT messageSeverity,
    VkDebugUtilsMessageTypeFlagsEXT messageTypes,
    VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
    void* userData
  ) {
    string message = new(pCallbackData->pMessage);
    if (messageTypes == VkDebugUtilsMessageTypeFlagsEXT.Validation) {
      if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error) {
        Logger.Error($"[Vulkan]: Validation: {messageSeverity} - {message}");
      } else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Warning) {
        Logger.Warn($"[Vulkan]: Validation: {messageSeverity} - {message}");
      }

      Debug.WriteLine($"[Vulkan]: Validation: {messageSeverity} - {message}");
    } else {
      if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error) {
        Logger.Error($"[Vulkan]: {messageSeverity} - {message}");
      } else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Warning) {
        Logger.Warn($"[Vulkan]: {messageSeverity} - {message}");
      }

      Debug.WriteLine($"[Vulkan]: {messageSeverity} - {message}");
    }

    return VK_FALSE;
  }

  private unsafe void CreateSurface() {
    VkSurfaceKHR surface;
    _window.CreateSurface(_vkInstance.Handle, &surface);
    _surface = surface;
  }

  private unsafe void PickPhysicalDevice() {
    _physicalDevice = DeviceHelper.GetPhysicalDevice(_vkInstance, _surface);
  }

  private unsafe void CreateLogicalDevice() {
    vkGetPhysicalDeviceProperties(_physicalDevice, out Properties);
    var queueFamilies = DeviceHelper.FindQueueFamilies(_physicalDevice, _surface);
    var availableDeviceExtensions = vkEnumerateDeviceExtensionProperties(_physicalDevice);

    HashSet<uint> uniqueQueueFamilies = [queueFamilies.graphicsFamily, queueFamilies.presentFamily];

    float priority = 1.0f;
    uint queueCount = 0;
    VkDeviceQueueCreateInfo[] queueCreateInfos = new VkDeviceQueueCreateInfo[2];

    foreach (uint queueFamily in uniqueQueueFamilies) {
      VkDeviceQueueCreateInfo queueCreateInfo = new() {
        queueFamilyIndex = queueFamily,
        queueCount = 1,
        pQueuePriorities = &priority
      };

      queueCreateInfos[queueCount++] = queueCreateInfo;
    }

    VkPhysicalDeviceFeatures deviceFeatures = new() {
      samplerAnisotropy = true,
      fillModeNonSolid = true,
      alphaToOne = true,
      sampleRateShading = true,
      multiDrawIndirect = true,
      geometryShader = true
    };

    VkDeviceCreateInfo createInfo = new() {
      queueCreateInfoCount = queueCount
    };
    fixed (VkDeviceQueueCreateInfo* ptr = queueCreateInfos) {
      createInfo.pQueueCreateInfos = ptr;
    }

    List<string> enabledExtensions = new() {
      VK_KHR_SWAPCHAIN_EXTENSION_NAME
    };

    using var deviceExtensionNames = new VkStringArray(enabledExtensions);

    createInfo.pEnabledFeatures = &deviceFeatures;
    createInfo.enabledExtensionCount = deviceExtensionNames.Length;
    createInfo.ppEnabledExtensionNames = deviceExtensionNames;

    var result = vkCreateDevice(_physicalDevice, &createInfo, null, out _logicalDevice);
    if (result != VkResult.Success) throw new Exception("Failed to create a device!");

    vkLoadDevice(_logicalDevice);

    vkGetDeviceQueue(_logicalDevice, queueFamilies.graphicsFamily, 0, out GraphicsQueue);
    vkGetDeviceQueue(_logicalDevice, queueFamilies.presentFamily, 0, out PresentQueue);
  }

  private unsafe void CreateCommandPool() {
    var queueFamilies = DeviceHelper.FindQueueFamilies(_physicalDevice, _surface);

    VkCommandPoolCreateInfo poolCreateInfo = new() {
      queueFamilyIndex = queueFamilies.graphicsFamily,
      flags = VkCommandPoolCreateFlags.Transient | VkCommandPoolCreateFlags.ResetCommandBuffer
    };

    var result = vkCreateCommandPool(_logicalDevice, &poolCreateInfo, null, out _commandPool);
    if (result != VkResult.Success) throw new Exception("Failed to create command pool!");
  }

  public unsafe void Dispose() {
    vkDestroyCommandPool(_logicalDevice, _commandPool);
    vkDestroyDevice(_logicalDevice);
    vkDestroySurfaceKHR(_vkInstance, _surface);
    vkDestroyDebugUtilsMessengerEXT(_vkInstance, _debugMessenger);
    vkDestroyInstance(_vkInstance);
  }

  public VkDevice LogicalDevice => _logicalDevice;
  public VkPhysicalDevice PhysicalDevice => _physicalDevice;
  public VkSurfaceKHR Surface => _surface;

  public VkCommandPool CommandPool {
    get {
      lock (_commandPoolLock) {
        return _commandPool;
      }
    }
  }
  public VkInstance VkInstance => _vkInstance;
}