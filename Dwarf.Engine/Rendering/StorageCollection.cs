using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public struct StorageData {
  public VkDescriptorSet[] Descriptors;
  public DwarfBuffer[] Buffers;
  public VkDescriptorType DescriptorType;
  public BufferUsage BufferUsage;
}

public class StorageCollection : IDisposable {
  private readonly VulkanDevice _device = null!;
  private readonly DescriptorPool _dynamicPool = null!;

  public StorageCollection(VulkanDevice device) {
    _device = device;

    _dynamicPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(10)
      .AddPoolSize(VkDescriptorType.StorageBuffer, 10)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();
  }

  public unsafe void CreateStorage(
    VulkanDevice device,
    VkDescriptorType descriptorType,
    BufferUsage usageType,
    int arraySize,
    ulong bufferSize,
    ulong bufferCount,
    DescriptorSetLayout layout,
    DescriptorPool pool,
    string storageName,
    ulong offsetAlignment,
    bool mapWholeBuffer = false
  ) {
    pool ??= _dynamicPool;

    var storage = new StorageData {
      Buffers = new DwarfBuffer[arraySize]
    };
    for (int i = 0; i < arraySize; i++) {
      storage.Buffers[i] = new(
        device,
        bufferSize,
        bufferCount,
        usageType,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
        device.Properties.limits.minStorageBufferOffsetAlignment
      );
      if (mapWholeBuffer) {
        storage.Buffers[i].Map();
      } else {
        storage.Buffers[i].Map(bufferSize * bufferCount);
      }
    }

    storage.Descriptors = new VkDescriptorSet[arraySize];
    for (int i = 0; i < storage.Descriptors.Length; i++) {
      var bufferInfo = mapWholeBuffer ?
        storage.Buffers[i].GetDescriptorBufferInfo() :
        storage.Buffers[i].GetDescriptorBufferInfo(bufferSize * bufferCount);
      _ = new VulkanDescriptorWriter(layout, pool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out storage.Descriptors[i]);
    }

    Storages.TryAdd(storageName, storage);
  }

  public unsafe void CheckSize(
    string key,
    int index,
    int elemCount,
    DescriptorSetLayout layout,
    bool mapWholeBuffer = false) {
    if (!Storages.TryGetValue(key, out var storageData)) return;
    if (storageData.Buffers.Length < index) return;
    var buff = storageData.Buffers[index];

    if (buff.GetBufferSize() < buff.GetAlignmentSize() * (ulong)elemCount ||
      buff.GetInstanceCount() > (ulong)elemCount
    ) {
      Storages[key].Buffers[index]?.Dispose();
      Storages[key].Buffers[index] = new(
        _device,
        (ulong)Unsafe.SizeOf<ObjectData>(),
        (ulong)elemCount,
        BufferUsage.StorageBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
      Storages[key].Buffers[index].Map();

      _dynamicPool.FreeDescriptors([Storages[key].Descriptors[index]]);

      var bufferInfo = Storages[key].Buffers[index].GetDescriptorBufferInfo();
      _ = new VulkanDescriptorWriter(layout, _dynamicPool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out Storages[key].Descriptors[index]);
    }
  }

  public void WriteBuffer(string key, int index, nint data, ulong size = VK_WHOLE_SIZE) {
    if (!Storages.TryGetValue(key, out var storage)) return;
    if (storage.Buffers[index] == null) return;
    Storages[key].Buffers[index].WriteToBuffer(data, size);
  }

  public VkDescriptorSet GetDescriptor(string key, int index) {
    return Storages[key].Descriptors[index];
  }

  private unsafe void RecreateBuffer(ref DwarfBuffer buffer, ulong newSize) {
    buffer.Resize(newSize);
  }

  private void RecreateBuffer(
    ref DwarfBuffer buffer,
    VulkanDevice device,
    BufferUsage usageType,
    ulong bufferSize,
    ulong bufferCount,
    string storageName,
    ulong offsetAlignment,
    bool mapWholeBuffer = false
  ) {
    buffer?.Dispose();

    buffer = new(
      device,
      bufferSize,
      bufferCount,
      usageType,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      offsetAlignment
    );

    buffer.Map(bufferSize * bufferCount);
  }

  public void Dispose() {
    _dynamicPool?.Dispose();
    foreach (var storage in Storages.Values) {
      foreach (var buffer in storage.Buffers) {
        buffer.Dispose();
      }
    }
  }

  public Dictionary<string, StorageData> Storages { get; private set; } = [];
}

