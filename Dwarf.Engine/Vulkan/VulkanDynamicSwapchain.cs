using System.Runtime.InteropServices;
using Dwarf.Extensions.Logging;
using Dwarf.Pathfinding;
using Dwarf.Utils;
using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanDynamicSwapchain : IDisposable {
    // https://github.com/SaschaWillems/Vulkan/blob/313ac10de4a765997ddf5202c599e4a0ca32c8ca/examples/dynamicrendering/dynamicrendering.cpp
    private const int MAX_FRAMES_IN_FLIGHT = 4;

    private readonly VulkanDevice _device;
    private VkSwapchainKHR _handle = VkSwapchainKHR.Null;

    public void BuildFrameBuffers() {

    }

    public void Dispose() {
        GC.SuppressFinalize(this);
    }
}