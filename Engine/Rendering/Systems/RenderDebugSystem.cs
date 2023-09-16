using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Physics;
using Dwarf.Vulkan;

using Dwarf.Engine;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.Systems;
public class RenderDebugSystem : SystemBase, IRenderSystem
{
  public RenderDebugSystem(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo)
  {

    VkDescriptorSetLayout[] descriptorSetLayouts = new VkDescriptorSetLayout[] {
      globalSetLayout,
    };

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(renderer.GetSwapchainRenderPass());
  }

  public unsafe void Render(FrameInfo frameInfo, Span<Entity> entities)
  {
    _pipeline.Bind(frameInfo.CommandBuffer);

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

    for (int i = 0; i < entities.Length; i++)
    {
      var targetEntity = entities[i].GetDrawable<IDebugRender3DObject>() as IDebugRender3DObject;
      if (targetEntity == null) continue;
      if (!targetEntity.Enabled) continue;

      var pushConstant = new ColliderMeshPushConstant();
      pushConstant.ModelMatrix = entities[i].GetComponent<Transform>().MatrixWithoutRotation;

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<ColliderMeshPushConstant>(),
        &pushConstant
      );

      if (!entities[i].CanBeDisposed)
      {
        for (uint x = 0; x < targetEntity!.MeshsesCount; x++)
        {
          if (!targetEntity.FinishedInitialization) continue;
          targetEntity.Bind(frameInfo.CommandBuffer, x);
          targetEntity.Draw(frameInfo.CommandBuffer, x);
        }
      }
    }
  }

  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts)
  {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<ColliderMeshPushConstant>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.setLayoutCount = (uint)layouts.Length;
    fixed (VkDescriptorSetLayout* ptr = layouts)
    {
      pipelineInfo.pSetLayouts = ptr;
    }
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private unsafe void CreatePipeline(VkRenderPass renderPass)
  {
    _pipeline?.Dispose();
    if (_pipelineConfigInfo == null)
    {
      _pipelineConfigInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "debug_vertex", "debug_fragment", pipelineConfig, new PipelineModelProvider());
  }

  public unsafe void Dispose()
  {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}
