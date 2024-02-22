using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class DescriptorSetLayout {
  private readonly VulkanDevice _device = null!;
  private Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings = new();
  private VkDescriptorSetLayout _descriptorSetLayout = VkDescriptorSetLayout.Null;
  public class Builder {
    private readonly VulkanDevice _device = null!;
    private Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings = new();
    public Builder(VulkanDevice device, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings) {
      _device = device;
      _bindings = bindings;
    }

    public Builder(VulkanDevice device) {
      _device = device;
    }

    public Builder AddBinding(
      uint binding,
      VkDescriptorType descriptorType,
      VkShaderStageFlags shaderStageFlags,
      uint count = 1
    ) {
      VkDescriptorSetLayoutBinding layoutBinding = new() {
        binding = binding,
        descriptorType = descriptorType,
        descriptorCount = count,
        stageFlags = shaderStageFlags
      };
      _bindings[binding] = layoutBinding;
      return this;
    }

    public DescriptorSetLayout Build() {
      return new DescriptorSetLayout(_device, _bindings);
    }

  }

  public unsafe DescriptorSetLayout(VulkanDevice device, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings) {
    _device = device;
    _bindings = bindings;

    VkDescriptorSetLayoutBinding[] setLayoutBindings = new VkDescriptorSetLayoutBinding[bindings.Count];
    for (uint i = 0; i < bindings.Count; i++) {
      setLayoutBindings[i] = bindings[i];
    }

    VkDescriptorSetLayoutCreateInfo descriptorSetLayoutInfo = new() {
      bindingCount = (uint)setLayoutBindings.Length
    };
    fixed (VkDescriptorSetLayoutBinding* ptr = setLayoutBindings) {
      descriptorSetLayoutInfo.pBindings = ptr;
    }

    vkCreateDescriptorSetLayout(_device.LogicalDevice, &descriptorSetLayoutInfo, null, out _descriptorSetLayout).CheckResult();
  }

  public VkDescriptorSetLayout GetDescriptorSetLayout() {
    return _descriptorSetLayout;
  }

  public Dictionary<uint, VkDescriptorSetLayoutBinding> Bindings => _bindings;

  public unsafe void Dispose() {
    vkDestroyDescriptorSetLayout(_device.LogicalDevice, _descriptorSetLayout);
  }
}