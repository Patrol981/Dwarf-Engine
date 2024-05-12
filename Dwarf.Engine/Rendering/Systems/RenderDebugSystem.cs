using System.Runtime.CompilerServices;

using Dwarf.EntityComponentSystem;
using Dwarf.Physics;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Systems;
public class RenderDebugSystem : SystemBase, IRenderSystem {
  public RenderDebugSystem(
    VulkanDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData<ColliderMeshPushConstant>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "debug_vertex",
      FragmentName = "debug_fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });

    // CreatePipelineLayout<ColliderMeshPushConstant>(descriptorSetLayouts);
    // CreatePipeline(renderer.GetSwapchainRenderPass(), "debug_vertex", "debug_fragment", new PipelineModelProvider());
  }

  public unsafe void Render(FrameInfo frameInfo, Span<Entity> entities) {
    // _pipeline.Bind(frameInfo.CommandBuffer);
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
      var targetEntity = entities[i].GetDrawable<IDebugRender3DObject>() as IDebugRender3DObject;
      if (targetEntity == null) continue;
      if (!targetEntity.Enabled) continue;

      var pushConstant = new ColliderMeshPushConstant {
        // pushConstant.ModelMatrix = entities[i].GetComponent<Transform>().MatrixWithoutRotation;
        // ModelMatrix = entities[i].GetComponent<Transform>().Matrix4

        ModelMatrix = entities[i].GetComponent<Rigidbody>().PrimitiveType == PrimitiveType.Convex ?
          entities[i].GetComponent<Transform>().MatrixWithAngleYRotation :
          entities[i].GetComponent<Transform>().MatrixWithAngleYRotation
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
