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

  public SimpleRenderSystem(Device device, VkRenderPass renderPass) {
    _device = device;
    CreatePipelineLayout();
    CreatePipeline(renderPass);
  }

  public void RenderEntities(FrameInfo frameInfo, Span<Entity> entities) {
    _pipeline.Bind(frameInfo.CommandBuffer);

    // var projectionView = camera.ProjectionMatrix() * camera.ViewMatrix();

    var speed = Time.DeltaTime;

    for (int i = 0; i < entities.Length; i++) {
      entities[i].GetComponent<Transform>().Rotation.Y += 20.0f * speed;

      var pushConstantData = new SimplePushConstantData();
      var model = entities[i].GetComponent<Transform>().Matrix4;
      pushConstantData.Transform = frameInfo.Camera.GetMVP(model);
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
      //entities[i].GetComponent<Model>()?.Bind(commandBuffer);
      //entities[i].GetComponent<Model>()?.Draw(commandBuffer);
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