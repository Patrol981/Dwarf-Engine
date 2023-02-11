using System.Linq;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class DescriptorSetLayout {
  private readonly Device _device = null!;
  private Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings = new();
  private VkDescriptorSetLayout _descriptorSetLayout = VkDescriptorSetLayout.Null;
  public class Builder {
    private readonly Device _device = null!;
    private Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings = new();
    public Builder(Device device, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings) {
      this._device = device;
      this._bindings = bindings;
    }

    public Builder(Device device) {
      this._device = device;
    }

    public Builder AddBinding(
      uint binding,
      VkDescriptorType descriptorType,
      VkShaderStageFlags shaderStageFlags,
      uint count = 1
    ) {
      VkDescriptorSetLayoutBinding layoutBinding = new();
      layoutBinding.binding = binding;
      layoutBinding.descriptorType = descriptorType;
      layoutBinding.descriptorCount = count;
      layoutBinding.stageFlags = shaderStageFlags;
      this._bindings[binding] = layoutBinding;
      return this;
    }

    public DescriptorSetLayout Build() {
      return new DescriptorSetLayout(this._device, this._bindings);
    }

  }

  public unsafe DescriptorSetLayout(Device device, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings) {
    this._device = device;
    this._bindings = bindings;

    VkDescriptorSetLayoutBinding[] setLayoutBindings = new VkDescriptorSetLayoutBinding[bindings.Count];
    for (uint i = 0; i < bindings.Count; i++) {
      setLayoutBindings[i] = bindings[i];
    }
    //foreach(var kv in bindings) {
    //setLayoutBindings.Add(kv.Value);
    //}

    VkDescriptorSetLayoutCreateInfo descriptorSetLayoutInfo = new();
    descriptorSetLayoutInfo.sType = VkStructureType.DescriptorSetLayoutCreateInfo;
    descriptorSetLayoutInfo.bindingCount = (uint)setLayoutBindings.Length;
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