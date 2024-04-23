using Dwarf.Utils;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.AbstractionLayer;

public unsafe class DwarfBuffer : IDisposable {
  public float LastTimeUsed = 0.0f;

  private readonly IDevice _device;
  // private nint _mapped;
  private void* _mapped;
  private readonly VkBuffer _buffer = VkBuffer.Null;
  private readonly VkDeviceMemory _memory = VkDeviceMemory.Null;

  private readonly ulong _bufferSize;
  private readonly ulong _instanceCount;
  private readonly ulong _instanceSize;
  private readonly ulong _alignmentSize;
  private readonly BufferUsage _usageFlags;
  private readonly MemoryProperty _memoryPropertyFlags;

  private readonly bool _isStagingBuffer = false;

  public DwarfBuffer(
    IDevice device,
    ulong instanceSize,
    ulong instanceCount,
    BufferUsage usageFlags,
    MemoryProperty propertyFlags,
    ulong minOffsetAlignment = 1,
    bool stagingBuffer = false
  ) {
    _device = device;
    _instanceSize = instanceSize;
    _instanceCount = instanceCount;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;

    _alignmentSize = GetAlignment(instanceSize, minOffsetAlignment);
    _bufferSize = _alignmentSize * _instanceCount;
    _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);

    _isStagingBuffer = stagingBuffer;
  }

  public DwarfBuffer(
    IDevice device,
    ulong bufferSize,
    BufferUsage usageFlags,
    MemoryProperty propertyFlags,
    ulong minOffsetAlignment = 1,
    bool stagingBuffer = false
  ) {
    _device = device;
    _instanceSize = bufferSize;
    _instanceCount = 1;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;
    _alignmentSize = bufferSize;
    _bufferSize = bufferSize;
    _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);

    _isStagingBuffer = stagingBuffer;
  }

  public void Map(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    fixed (void** ptr = &_mapped) {
      vkMapMemory(_device.LogicalDevice, _memory, offset, size, VkMemoryMapFlags.None, ptr).CheckResult();
    }
  }

  public static void Map(DwarfBuffer buff, ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    fixed (void** ptr = &buff._mapped) {
      vkMapMemory(buff._device.LogicalDevice, buff._memory, offset, size, VkMemoryMapFlags.None, ptr).CheckResult();
    }
  }

  public void Unmap() {
    // Logger.Info($"Mapped NULL : {_mapped == null}");
    if (_mapped != null) {
      vkUnmapMemory(_device.LogicalDevice, _memory);
      _mapped = null;
    }

    /*
    if (_mapped != nint.Zero) {
      
      _mapped = nint.Zero;
    }
    */
  }

  public void WriteToBuffer(nint data, ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    if (size == VK_WHOLE_SIZE) {
      MemoryUtils.MemCopy(_mapped, (void*)data, (int)_bufferSize);
    } else {
      if (size <= 0) {
        // Logger.Warn("[Buffer] Size of an write is less or equal to 0");
        return;
      }
      char* memOffset = (char*)_mapped;
      memOffset += offset;
      MemoryUtils.MemCopy((nint)memOffset, data, (int)size);
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

  public void WrtieToIndex(nint data, int index) {
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

  public void* GetMappedMemory() {
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

  public BufferUsage GetVkBufferUsageFlags() {
    return _usageFlags;
  }

  public MemoryProperty GetVkMemoryPropertyFlags() {
    return _memoryPropertyFlags;
  }

  public ulong GetBufferSize() {
    return _bufferSize;
  }

  private static ulong GetAlignment(ulong instanceSize, ulong minOffsetAlignment) {
    return minOffsetAlignment > 0 ? (instanceSize + minOffsetAlignment - 1) & ~(minOffsetAlignment - 1) : instanceSize;
  }

  public void FreeMemory() {
    vkFreeMemory(_device.LogicalDevice, _memory);
  }

  public void DestoryBuffer() {
    vkDestroyBuffer(_device.LogicalDevice, _buffer);
  }

  public void Dispose() {
    if (!_isStagingBuffer) {
      _device.WaitDevice();
      _device.WaitQueue();
    }
    Unmap();
    DestoryBuffer();
    FreeMemory();
  }
}