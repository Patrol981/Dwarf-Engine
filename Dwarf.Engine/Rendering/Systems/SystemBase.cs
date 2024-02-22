
using Dwarf.Engine.Rendering;
using Dwarf.Vulkan;

using Dwarf.Engine;

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;
using System.Runtime.CompilerServices;
using DwarfEngine.Vulkan;
using Dwarf.Extensions.Logging;
using Dwarf.Engine.EntityComponentSystem;

namespace Dwarf.Engine;
public abstract class SystemBase {
  protected readonly VulkanDevice _device = null!;
  protected readonly Renderer _renderer = null!;
  protected VkDescriptorSetLayout _globalDescriptorSetLayout;
  protected PipelineConfigInfo _pipelineConfigInfo;
  protected VkPipelineLayout _pipelineLayout;
  protected Pipeline _pipeline = null!;

  // protected Vulkan.Buffer[] _buffer = new Vulkan.Buffer[0];
  protected DescriptorPool _descriptorPool = null!;
  protected DescriptorPool _texturePool = null!;
  protected DescriptorSetLayout _setLayout = null!;
  protected DescriptorSetLayout _textureSetLayout = null!;
  protected VkDescriptorSet[] _descriptorSets = [];

  protected int _texturesCount = 0;

  public SystemBase(
    VulkanDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) {
    _device = device;
    _renderer = renderer;
    _globalDescriptorSetLayout = globalSetLayout;
    _pipelineConfigInfo = configInfo;
  }

  public void BindBuffer(FrameInfo frameInfo) {
    // vkCmdBindVertexBuffers(frameInfo.CommandBuffer, 0, 1, )
  }

  #region Pipeline

  protected unsafe void CreatePipelineLayout<T>(VkDescriptorSetLayout[] layouts) {
    CreatePipelineLayoutBase(layouts, out var pipelineInfo);
    var push = CreatePushConstants<T>();
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &push;
    FinalizePipelineLayout(&pipelineInfo);
  }

  protected unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    CreatePipelineLayoutBase(layouts, out var pipelineInfo);
    FinalizePipelineLayout(&pipelineInfo);
  }

  protected unsafe void CreatePipelineLayoutBase(VkDescriptorSetLayout[] layouts, out VkPipelineLayoutCreateInfo pipelineInfo) {
    pipelineInfo = new() {
      setLayoutCount = (uint)layouts.Length
    };
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
  }

  protected unsafe void FinalizePipelineLayout(VkPipelineLayoutCreateInfo* pipelineInfo) {
    vkCreatePipelineLayout(_device.LogicalDevice, pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  protected unsafe VkPushConstantRange CreatePushConstants<T>() {
    VkPushConstantRange pushConstantRange = new() {
      stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      offset = 0,
      size = (uint)Unsafe.SizeOf<T>()
    };

    return pushConstantRange;
  }

  protected void CreatePipeline(
    VkRenderPass renderPass,
    string vertexName,
    string fragmentName,
    PipelineProvider pipelineProvider
  ) {
    _pipeline?.Dispose();
    _pipelineConfigInfo ??= new PipelineConfigInfo();
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, vertexName, fragmentName, pipelineConfig, pipelineProvider);
  }

  #endregion

  public virtual unsafe void Dispose() {
    _device.WaitQueue();
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    _descriptorPool?.Dispose();
    _texturePool?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}
