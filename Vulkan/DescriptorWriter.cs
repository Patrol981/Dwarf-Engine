using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class DescriptorWriter {
  private readonly DescriptorSetLayout _setLayout;
  private readonly DescriptorPool _pool;
  private VkWriteDescriptorSet[] _writes = [];
  public DescriptorWriter(DescriptorSetLayout setLayout, DescriptorPool pool) {
    _setLayout = setLayout;
    _pool = pool;
  }

  public unsafe DescriptorWriter WriteBuffer(uint binding, VkDescriptorBufferInfo* bufferInfo) {
    var bindingDescription = _setLayout.Bindings[binding];

    VkWriteDescriptorSet write = new() {
      descriptorType = bindingDescription.descriptorType,
      dstBinding = binding,
      pBufferInfo = bufferInfo,
      descriptorCount = 1
    };

    var tmp = _writes.ToList();
    tmp.Add(write);
    _writes = tmp.ToArray();
    return this;
  }

  public unsafe DescriptorWriter WriteImage(uint binding, VkDescriptorImageInfo* imageInfo) {
    var bindingDescription = _setLayout.Bindings[binding];

    VkWriteDescriptorSet write = new() {
      descriptorType = bindingDescription.descriptorType,
      dstBinding = binding,
      pImageInfo = imageInfo,
      descriptorCount = 1
    };

    var tmp = _writes.ToList();
    tmp.Add(write);
    _writes = tmp.ToArray();
    return this;
  }

  public bool Build(out VkDescriptorSet set) {
    bool success = _pool.AllocateDescriptor(_setLayout.GetDescriptorSetLayout(), out set);
    if (!success) {
      return false;
    }
    Overwrite(ref set);

    return true;
  }

  public unsafe void Overwrite(ref VkDescriptorSet set) {
    for (uint i = 0; i < _writes.Length; i++) {
      _writes[i].dstSet = set;
    }
    vkUpdateDescriptorSets(_pool.Device.LogicalDevice, _writes);
  }

  public void Free() {
  }
}