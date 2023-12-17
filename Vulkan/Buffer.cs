using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public unsafe class Buffer : IDisposable {
  public float LastTimeUsed = 0.0f;

  private Device _device;
  private IntPtr _mapped;
  private VkBuffer _buffer = VkBuffer.Null;
  private VkDeviceMemory _memory = VkDeviceMemory.Null;

  private ulong _bufferSize;
  private ulong _instanceCount;
  private ulong _instanceSize;
  private ulong _alignmentSize;
  private VkBufferUsageFlags _usageFlags;
  private VkMemoryPropertyFlags _memoryPropertyFlags;

  public Buffer(
    Device device,
    ulong instanceSize,
    ulong instanceCount,
    VkBufferUsageFlags usageFlags,
    VkMemoryPropertyFlags propertyFlags,
    ulong minOffsetAlignment = 1
  ) {
    _device = device;
    _instanceSize = instanceSize;
    _instanceCount = instanceCount;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;

    _alignmentSize = GetAlignment(instanceSize, minOffsetAlignment);
    _bufferSize = _alignmentSize * _instanceCount;
    _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);
  }

  public Buffer(
    Device device,
    ulong bufferSize,
    VkBufferUsageFlags usageFlags,
    VkMemoryPropertyFlags propertyFlags,
    ulong minOffsetAlignment = 1
  ) {
    _device = device;
    _instanceSize = 0;
    _instanceCount = 0;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;
    _alignmentSize = 0;
    _bufferSize = bufferSize;
    _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);
  }

  public void Map(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    fixed (void* ptr = &_mapped) {
      vkMapMemory(_device.LogicalDevice, _memory, offset, size, VkMemoryMapFlags.None, ptr).CheckResult();
    }
  }

  public static void Map(Buffer buff, ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    fixed (void* ptr = &buff._mapped) {
      vkMapMemory(buff._device.LogicalDevice, buff._memory, offset, size, VkMemoryMapFlags.None, ptr).CheckResult();
    }
  }

  public void Unmap() {
    if (_mapped != IntPtr.Zero) {
      vkUnmapMemory(_device.LogicalDevice, _memory);
      _mapped = IntPtr.Zero;
      // GC.Collect();
    }
  }

  public void WriteToBuffer(IntPtr data, ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    if (size == VK_WHOLE_SIZE) {
      VkUtils.MemCopy((IntPtr)_mapped, data, (int)_bufferSize);
    } else {
      char* memOffset = (char*)_mapped;
      memOffset += offset;
      VkUtils.MemCopy((IntPtr)memOffset, data, (int)size);
    }
  }
  public VkResult Flush(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    VkMappedMemoryRange mappedRange = new() {
      memory = _memory,
      offset = offset,
      size = size
    };
    return vkFlushMappedMemoryRanges(_device.LogicalDevice, 1, &mappedRange);
  }

  public VkDescriptorBufferInfo GetDescriptorBufferInfo(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    VkDescriptorBufferInfo bufferInfo = new() {
      buffer = _buffer,
      offset = offset,
      range = size
    };
    return bufferInfo;
  }

  public VkResult Invalidate(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    VkMappedMemoryRange mappedRange = new() {
      memory = _memory,
      offset = offset,
      size = size
    };
    return vkInvalidateMappedMemoryRanges(_device.LogicalDevice, 1, &mappedRange);
  }

  public void WrtieToIndex(IntPtr data, int index) {
    WriteToBuffer(data, _instanceSize, (ulong)index * _alignmentSize);
  }

  public VkResult FlushIndex(int index) {
    return Flush(_alignmentSize, (ulong)index * _alignmentSize);
  }

  public VkDescriptorBufferInfo GetVkDescriptorBufferInfoForIndex(int index) {
    return GetDescriptorBufferInfo(_alignmentSize, (ulong)index * _alignmentSize);
  }

  public VkResult InvalidateIndex(int index) {
    return Invalidate(_alignmentSize, (ulong)index * _alignmentSize);
  }

  public VkBuffer GetBuffer() {
    return _buffer;
  }

  public VkDeviceMemory GetVkDeviceMemory() {
    return _memory;
  }

  public IntPtr GetMappedMemory() {
    return _mapped;
  }

  public ulong GetInstanceCount() {
    return _instanceCount;
  }

  public ulong GetInstanceSize() {
    return _instanceSize;
  }

  public ulong GetAlignmentSize() {
    return _alignmentSize;
  }

  public VkBufferUsageFlags GetVkBufferUsageFlags() {
    return _usageFlags;
  }

  public VkMemoryPropertyFlags GetVkMemoryPropertyFlags() {
    return _memoryPropertyFlags;
  }

  public ulong GetBufferSize() {
    return _bufferSize;
  }

  private static ulong GetAlignment(ulong instanceSize, ulong minOffsetAlignment) {
    if (minOffsetAlignment > 0) {
      return (instanceSize + minOffsetAlignment - 1) & ~(minOffsetAlignment - 1);
      // return (instanceSize + minOffsetAlignment - 1) / minOffsetAlignment * minOffsetAlignment;
    }
    return instanceSize;
  }

  public void FreeMemory() {
    vkFreeMemory(_device.LogicalDevice, _memory);
  }

  public void DestoryBuffer() {
    vkDestroyBuffer(_device.LogicalDevice, _buffer);
  }

  public void Dispose() {
    Unmap();
    DestoryBuffer();
    FreeMemory();
  }
}