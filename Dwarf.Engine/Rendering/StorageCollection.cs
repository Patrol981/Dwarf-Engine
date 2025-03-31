using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
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
  private readonly VmaAllocator _vmaAllocator = VmaAllocator.Null;
  private readonly DescriptorPool _dynamicPool = null!;

  public StorageCollection(VmaAllocator vmaAllocator, VulkanDevice device) {
    _device = device;
    _vmaAllocator = vmaAllocator;

    _dynamicPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(30)
      .AddPoolSize(VkDescriptorType.StorageBuffer, 30)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet | VkDescriptorPoolCreateFlags.UpdateAfterBind)
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
    if (bufferCount == 0) bufferCount = 1;

    pool ??= _dynamicPool;

    var storage = new StorageData {
      Buffers = new DwarfBuffer[arraySize]
    };
    for (int i = 0; i < arraySize; i++) {
      storage.Buffers[i] = new(
        _vmaAllocator,
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
    if (elemCount < 1) return;
    var buff = storageData.Buffers[index];

    if (buff.GetBufferSize() < buff.GetAlignmentSize() * (ulong)elemCount ||
      buff.GetInstanceCount() > (ulong)elemCount
    ) {
      Storages[key].Buffers[index]?.Dispose();
      Storages[key].Buffers[index] = new(
        _vmaAllocator,
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

      Logger.Info($"[Storage Collection] Updated Sizes of {key}[{index}].");
    }
  }

  public void WriteBuffer(string key, int index, nint data, ulong size = VK_WHOLE_SIZE) {
    if (!Storages.TryGetValue(key, out var storage)) return;
    if (storage.Buffers[index] == null) return;
    Application.Instance.Mutex.WaitOne();
    Storages[key].Buffers[index].WriteToBuffer(data, size);
    Application.Instance.Mutex.ReleaseMutex();
  }

  public VkDescriptorSet GetDescriptor(string key, int index) {
    // Storages[key].Descriptors[index]
    return Storages.TryGetValue(key, out var storageData)
      ? storageData.Descriptors[index] != VkDescriptorSet.Null ? storageData.Descriptors[index] : VkDescriptorSet.Null
      : VkDescriptorSet.Null;
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

