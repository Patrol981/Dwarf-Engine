using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanDescriptorWriter {
  private readonly unsafe DescriptorSetLayout _setLayout;
  private readonly unsafe DescriptorPool _pool;
  private VkWriteDescriptorSet[] _writes = [];
  public VulkanDescriptorWriter(DescriptorSetLayout setLayout, DescriptorPool pool) {
    _setLayout = setLayout;
    _pool = pool;
  }

  public unsafe VulkanDescriptorWriter(nint setLayout, nint pool) {
    // _setLayout = &setLayout;
    // _pool = pool;
  }


  public unsafe VulkanDescriptorWriter WriteBuffer(uint binding, VkDescriptorBufferInfo* bufferInfo) {
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

  public unsafe VulkanDescriptorWriter WriteImage(uint binding, VkDescriptorImageInfo* imageInfo) {
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

  public unsafe bool Build(out VkDescriptorSet set) {
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