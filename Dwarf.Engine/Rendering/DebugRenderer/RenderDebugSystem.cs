using System.Runtime.CompilerServices;

using Dwarf.EntityComponentSystem;
using Dwarf.Physics;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.DebugRenderer;
public class RenderDebugSystem : SystemBase, IRenderSystem {
  public RenderDebugSystem(
    VmaAllocator vmaAllocator,
    VulkanDevice device,
    IRenderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData<ColliderMeshPushConstant>(new() {
      RenderPass = renderer.GetPostProcessingPass(),
      VertexName = "debug_vertex",
      FragmentName = "debug_fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public unsafe void Render(FrameInfo frameInfo, ReadOnlySpan<Entity> entities) {
    BindPipeline(frameInfo.CommandBuffer);

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      0,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      if (entities[i].GetDrawable<IDebugRenderObject>() is not IDebugRenderObject targetEntity) continue;
      if (!targetEntity.Enabled) continue;

      var pushConstant = new ColliderMeshPushConstant {
        ModelMatrix = entities[i].GetComponent<Transform>().MatrixWithAngleYRotationWithoutScale
      };

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<ColliderMeshPushConstant>(),
        &pushConstant
      );

      if (!entities[i].CanBeDisposed) {
        for (uint x = 0; x < targetEntity!.MeshsesCount; x++) {
          if (!targetEntity.FinishedInitialization) continue;
          targetEntity.Bind(frameInfo.CommandBuffer, x);
          targetEntity.Draw(frameInfo.CommandBuffer, x);
        }
      }
    }
  }

  public override unsafe void Dispose() {
    base.Dispose();
  }

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    throw new NotImplementedException();
  }
}
