using Dwarf.Utils;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.AbstractionLayer;

public unsafe class DwarfBuffer : IDisposable {
  public float LastTimeUsed = 0.0f;

  private readonly IDevice _device;
  private void* _mapped;
  private VkBuffer _buffer = VkBuffer.Null;
  private VkDeviceMemory _memory = VkDeviceMemory.Null;

  private ulong _bufferSize;
  private ulong _instanceCount;
  private readonly ulong _instanceSize;
  private readonly ulong _alignmentSize;
  private readonly ulong _offsetAlignment;
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
    _offsetAlignment = minOffsetAlignment;

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
    bool stagingBuffer = false
  ) {
    _device = device;
    _instanceSize = bufferSize;
    _instanceCount = 1;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;
    _alignmentSize = bufferSize;
    _offsetAlignment = 1;
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
    if (_mapped != null) {
      vkUnmapMemory(_device.LogicalDevice, _memory);
      _mapped = null;
    }
  }

  public void WriteToBuffer(nint data, ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    if (size == VK_WHOLE_SIZE) {
      MemoryUtils.MemCopy(_mapped, (void*)data, (int)_bufferSize);
    } else {
      if (size <= 0) {
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

  public ulong GetOffsetAlignment() {
    return _offsetAlignment;
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

  public void Resize(ulong newCount) {
    /*
    _device = device;
    _instanceSize = bufferSize;
    _instanceCount = 1;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;
    _alignmentSize = bufferSize;
    _offsetAlignment = 1;
    _bufferSize = bufferSize;
    _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);

    _isStagingBuffer = stagingBuffer;
    */

    /*
     _device = device;
    _instanceSize = instanceSize;
    _instanceCount = instanceCount;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;
    _offsetAlignment = minOffsetAlignment;

    _alignmentSize = GetAlignment(instanceSize, minOffsetAlignment);
    _bufferSize = _alignmentSize * _instanceCount;
    _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);
    */

    _device.WaitDevice();
    _device.WaitQueue();

    Unmap();

    var oldSize = _bufferSize;

    _instanceCount = newCount;
    _bufferSize = _alignmentSize * _instanceCount;

    VkBufferCreateInfo bufferCreateInfo = new() {
      sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
      size = _bufferSize,
      usage = VK_BUFFER_USAGE_STORAGE_BUFFER_BIT | VK_BUFFER_USAGE_TRANSFER_SRC_BIT | VK_BUFFER_USAGE_TRANSFER_DST_BIT,
      sharingMode = VK_SHARING_MODE_EXCLUSIVE
    };

    VkBuffer newBuffer;
    vkCreateBuffer(_device.LogicalDevice, &bufferCreateInfo, null, &newBuffer);

    VkMemoryRequirements memRequirements;
    vkGetBufferMemoryRequirements(_device.LogicalDevice, newBuffer, &memRequirements);

    VkMemoryAllocateInfo allocInfo = new() {
      sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
      allocationSize = memRequirements.size,
      memoryTypeIndex = _device.FindMemoryType(memRequirements.memoryTypeBits, MemoryProperty.HostVisible | MemoryProperty.HostCoherent)
    };

    VkDeviceMemory newBufferMemory;
    vkAllocateMemory(_device.LogicalDevice, &allocInfo, null, &newBufferMemory);
    vkBindBufferMemory(_device.LogicalDevice, newBuffer, newBufferMemory, 0);

    void* oldData;
    vkMapMemory(_device.LogicalDevice, _memory, 0, oldSize, 0, &oldData);

    void* newData;
    vkMapMemory(_device.LogicalDevice, newBufferMemory, 0, oldSize, 0, &newData);

    // MemoryUtils.MemCopy(_mapped, (void*)data, (int)_bufferSize);
    MemoryUtils.MemCopy((nint)newData, (nint)oldData, (int)oldSize);

    vkUnmapMemory(_device.LogicalDevice, _memory);
    vkUnmapMemory(_device.LogicalDevice, newBufferMemory);

    vkDestroyBuffer(_device.LogicalDevice, _buffer, null);
    vkFreeMemory(_device.LogicalDevice, _memory, null);

    _buffer = newBuffer;
    _memory = newBufferMemory;

    Map();
  }

  public void Dispose() {
    if (!_isStagingBuffer) {
      _device.WaitAllQueues();
    }
    Unmap();
    DestoryBuffer();
    FreeMemory();
  }
}