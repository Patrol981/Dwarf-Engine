using Dwarf.AbstractionLayer;

using Vortice.Vulkan;

namespace Dwarf.Engine;

public interface IDevice : IDisposable {
  public void CreateBuffer(ulong size,
    BufferUsage uFlags,
    MemoryProperty pFlags,
    out VkBuffer buffer,
    out VkDeviceMemory bufferMemory
  );


}