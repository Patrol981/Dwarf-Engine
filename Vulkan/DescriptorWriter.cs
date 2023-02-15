using Dwarf.Extensions.Logging;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class DescriptorWriter {
  private readonly DescriptorSetLayout _setLayout;
  private readonly DescriptorPool _pool;
  private VkWriteDescriptorSet[] _writes = new VkWriteDescriptorSet[0];
  public DescriptorWriter(DescriptorSetLayout setLayout, DescriptorPool pool) {
    _setLayout = setLayout;
    _pool = pool;
  }

  public unsafe DescriptorWriter WriteBuffer(uint binding, VkDescriptorBufferInfo* bufferInfo) {
    var bindingDescription = _setLayout.Bindings[binding];

    if (bindingDescription.descriptorCount == 1) {
      // Logger.Warn("Binding single descriptor info, but binding expects multiple");
      // return this;
    }

    VkWriteDescriptorSet write = new();
    write.sType = VkStructureType.WriteDescriptorSet;
    write.descriptorType = bindingDescription.descriptorType;
    write.dstBinding = binding;
    write.pBufferInfo = bufferInfo;
    write.descriptorCount = 1;

    var tmp = _writes.ToList();
    tmp.Add(write);
    _writes = tmp.ToArray();
    return this;
  }

  public unsafe DescriptorWriter WriteImage(uint binding, VkDescriptorImageInfo* imageInfo) {
    var bindingDescription = _setLayout.Bindings[binding];

    if (bindingDescription.descriptorCount == 1) {
      Logger.Warn("Binding single descriptor info, but binding expects multiple");
      // return this;
    }

    VkWriteDescriptorSet write = new();
    write.sType = VkStructureType.WriteDescriptorSet;
    write.descriptorType = bindingDescription.descriptorType;
    write.dstBinding = binding;
    write.pImageInfo = imageInfo;
    write.descriptorCount = 1;

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
    //fixed (VkWriteDescriptorSet* ptr = _writes) {
    //vkUpdateDescriptorSets(_pool.Device.LogicalDevice, _writes.Length, ptr, 0, null);
    //}
  }

  public void Free() {
  }
}