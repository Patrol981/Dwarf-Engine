using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class DescriptorPool : IDisposable {
  private readonly Device _device;
  private VkDescriptorPool _descriptorPool;
  public class Builder {
    private readonly Device _device;
    private VkDescriptorPoolSize[] _poolSizes = new VkDescriptorPoolSize[0];
    private uint _maxSets = 1000;
    private VkDescriptorPoolCreateFlags _poolFlags = 0;

    public Builder(Device device, uint maxSets, VkDescriptorPoolCreateFlags poolFlags, VkDescriptorPoolSize[] poolSizes) {
      this._device = device;
      this._maxSets = maxSets;
      this._poolSizes = poolSizes;
      this._poolFlags = poolFlags;
    }

    public Builder(Device device) {
      this._device = device;
    }

    public Builder AddPoolSize(VkDescriptorType descriptorType, uint count) {
      VkDescriptorPoolSize poolSize = new();
      poolSize.descriptorCount = count;
      poolSize.type = descriptorType;
      var tmpList = _poolSizes.ToList();
      tmpList.Add(poolSize);
      _poolSizes = tmpList.ToArray();
      return this;
    }

    public Builder SetPoolFlags(VkDescriptorPoolCreateFlags flags) {
      _poolFlags = flags;
      return this;
    }

    public Builder SetMaxSets(uint count) {
      _maxSets = count;
      return this;
    }

    public DescriptorPool Build() {
      return new DescriptorPool(_device, _maxSets, _poolFlags, _poolSizes);
    }
  }

  public unsafe DescriptorPool(
    Device device,
    uint maxSets,
    VkDescriptorPoolCreateFlags poolFlags,
    VkDescriptorPoolSize[] poolSizes
  ) {
    _device = device;

    VkDescriptorPoolCreateInfo descriptorPoolInfo = new();
    descriptorPoolInfo.sType = VkStructureType.DescriptorPoolCreateInfo;
    descriptorPoolInfo.poolSizeCount = (uint)poolSizes.Length;
    fixed (VkDescriptorPoolSize* ptr = poolSizes) {
      descriptorPoolInfo.pPoolSizes = ptr;
    }
    descriptorPoolInfo.maxSets = maxSets;
    descriptorPoolInfo.flags = poolFlags;

    vkCreateDescriptorPool(_device.LogicalDevice, &descriptorPoolInfo, null, out _descriptorPool).CheckResult();
  }

  public unsafe bool AllocateDescriptor(VkDescriptorSetLayout descriptorSetLayout, out VkDescriptorSet descriptorSet) {
    VkDescriptorSetAllocateInfo allocInfo = new();
    allocInfo.sType = VkStructureType.DescriptorSetAllocateInfo;
    allocInfo.descriptorPool = _descriptorPool;
    allocInfo.pSetLayouts = &descriptorSetLayout;
    allocInfo.descriptorSetCount = 1;

    fixed (VkDescriptorSet* ptr = &descriptorSet) {
      var result = vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, ptr);
      if (result != VkResult.Success) {
        return false;
      }
      return true;
    }
  }

  public unsafe void FreeDescriptors(VkDescriptorSet[] descriptorSets) {
    fixed (VkDescriptorSet* ptr = descriptorSets) {
      vkFreeDescriptorSets(_device.LogicalDevice, _descriptorPool, descriptorSets.Length, ptr).CheckResult();
    }
  }

  public unsafe void ResetPool() {
    vkResetDescriptorPool(_device.LogicalDevice, _descriptorPool, 0).CheckResult();
  }

  public Device Device => _device;

  public unsafe void Dispose() {
    vkDestroyDescriptorPool(_device.LogicalDevice, _descriptorPool);
  }
}