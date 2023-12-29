using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Physics;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.Systems;
public class RenderDebugSystem : SystemBase, IRenderSystem {
  public RenderDebugSystem(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    CreatePipelineLayout<ColliderMeshPushConstant>(descriptorSetLayouts);
    CreatePipeline(renderer.GetSwapchainRenderPass(), "debug_vertex", "debug_fragment", new PipelineModelProvider());
  }

  public unsafe void Render(FrameInfo frameInfo, Span<Entity> entities) {
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

    for (int i = 0; i < entities.Length; i++) {
      var targetEntity = entities[i].GetDrawable<IDebugRender3DObject>() as IDebugRender3DObject;
      if (targetEntity == null) continue;
      if (!targetEntity.Enabled) continue;

      var pushConstant = new ColliderMeshPushConstant {
        // pushConstant.ModelMatrix = entities[i].GetComponent<Transform>().MatrixWithoutRotation;
        // ModelMatrix = entities[i].GetComponent<Transform>().Matrix4

        ModelMatrix = entities[i].GetComponent<Rigidbody>().PrimitiveType == PrimitiveType.Convex ?
          entities[i].GetComponent<Transform>().Matrix4 :
          entities[i].GetComponent<Transform>().MatrixWithoutRotation
      };

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        _pipelineLayout,
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
