using Dwarf.Engine.Windowing;

using Vortice.Vulkan;

namespace Dwarf.Engine.AbstractionLayer;

public interface IDevice : IDisposable {
  public void CreateBuffer(
    ulong size,
    BufferUsage uFlags,
    MemoryProperty pFlags,
    out VkBuffer buffer,
    out VkDeviceMemory bufferMemory
  );

  public Task CopyBuffer(ulong srcBuffer, ulong dstBuffer, ulong size);

  public void WaitQueue();
  public void WaitDevice();

  public nint BeginSingleTimeCommands();
  public void EndSingleTimeCommands(nint commandBuffer);
  public uint FindMemoryType(uint typeFilter, MemoryProperty properties);

  public ulong CommandPool { get; }

  public IntPtr LogicalDevice { get; }
  public IntPtr PhysicalDevice { get; }

  public nint GraphicsQueue { get; }
  public nint PresentQueue { get; }
}