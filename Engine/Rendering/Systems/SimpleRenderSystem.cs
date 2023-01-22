using System.Runtime.CompilerServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public unsafe class SimpleRenderSystem : IDisposable {
  private Device _device = null!;
  private Pipeline _pipeline = null!;
  private VkPipelineLayout _pipelineLayout;

  public SimpleRenderSystem(Device device, VkRenderPass renderPass) {
    _device = device;
    CreatePipelineLayout();
    CreatePipeline(renderPass);
  }

  public void RenderEntities(VkCommandBuffer commandBuffer, Span<Entity> entities) {
    _pipeline.Bind(commandBuffer);

    for (int i = 0; i < entities.Length; i++) {
      //var x = entities[i].GetComponent<Transform2D>().Rotation + 0.01f;
      //entities[i].GetComponent<Transform2D>().Rotation = x % (MathF.PI * 2);

      var pushConstantData = new SimplePushConstantData();
      pushConstantData.Transform = entities[i].GetComponent<Transform2D>().Matrix4;
      pushConstantData.Offset = entities[i].GetComponent<Transform2D>().Translation;
      pushConstantData.Color = entities[i].GetComponent<Material>().GetColor();

      vkCmdPushConstants(
        commandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<SimplePushConstantData>(),
        &pushConstantData
      );

      entities[i].GetComponent<Model>()?.Bind(commandBuffer);
      entities[i].GetComponent<Model>()?.Draw(commandBuffer);
    }
  }

  private void CreatePipelineLayout() {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<SimplePushConstantData>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
    pipelineInfo.setLayoutCount = 0;
    pipelineInfo.pSetLayouts = null;
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