using System.Runtime.CompilerServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public unsafe class SimpleRenderSystem : IDisposable {
  private Device _device = null!;
  private Pipeline _pipeline = null!;
  private VkPipelineLayout _pipelineLayout;

  public SimpleRenderSystem(Device device, VkRenderPass renderPass, VkDescriptorSetLayout globalSetLayout) {
    _device = device;
    CreatePipelineLayout(globalSetLayout);
    CreatePipeline(renderPass);
  }

  public void RenderEntities(FrameInfo frameInfo, Span<Entity> entities) {
    _pipeline.Bind(frameInfo.CommandBuffer);

    if (frameInfo.GlobalDescriptorSet.IsNotNull) {
      // Console.WriteLine(frameInfo.GlobalDescriptorSet.IsNotNull);
    }

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      _pipelineLayout,
      0,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    for (int i = 0; i < entities.Length; i++) {
      var pushConstantData = new SimplePushConstantData();
      var model = entities[i].GetComponent<Transform>().Matrix4;
      // pushConstantData.ModelMatrix = frameInfo.Camera.GetMVP(model);
      pushConstantData.ModelMatrix = model;
      pushConstantData.NormalMatrix = entities[i].GetComponent<Transform>().NormalMatrix;
      //pushConstantData.Color = entities[i].GetComponent<Material>().GetColor();

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<SimplePushConstantData>(),
        &pushConstantData
      );

      var entity = entities[i].GetComponent<Model>();
      for (uint x = 0; x < entity.MeshsesCount; x++) {
        entity.Bind(frameInfo.CommandBuffer, x);
        entity.Draw(frameInfo.CommandBuffer, x);
      }
    }
  }

  private void CreatePipelineLayout(VkDescriptorSetLayout globalSetLayout) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<SimplePushConstantData>();

    VkDescriptorSetLayout[] descriptorSetLayouts = new VkDescriptorSetLayout[] { globalSetLayout };

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
    pipelineInfo.setLayoutCount = (uint)descriptorSetLayouts.Length;
    fixed (VkDescriptorSetLayout* ptr = descriptorSetLayouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    PipelineConfigInfo configInfo = new();
    var pipelineConfig = Pipeline.DefaultConfigInfo(configInfo);
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "vertex", "fragment", pipelineConfig);
  }

  public void Dispose() {
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}