using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.Globals;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Systems;

public struct GuizmoIndirectBatch {
  public uint First;
  public uint Count;
};

public class GuizmoRenderSystem : SystemBase {
  private readonly unsafe GuizmoBufferObject* _bufferObject =
    (GuizmoBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<GuizmoBufferObject>());

  public GuizmoRenderSystem(
    VmaAllocator vmaAllocator,
    IDevice device,
    IRenderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData<GuizmoBufferObject>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "guizmo_vertex",
      FragmentName = "guizmo_fragment",
      PipelineProvider = new GuizmoPipelineProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public void Render(FrameInfo frameInfo) {
    if (Guizmos.Data.Count < 1) return;

    BindPipeline(frameInfo.CommandBuffer);
    unsafe {
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
    }

    var guizmos = Guizmos.Data;
    var perFrameGuizmos = Guizmos.PerFrameGuizmos;

    Draw(frameInfo, guizmos);

    if (perFrameGuizmos != null && perFrameGuizmos.Length > 0) {
      // Draw(frameInfo, perFrameGuizmos);
      // Guizmos.Free();
    }
  }

  private void Draw(FrameInfo frameInfo, List<Guizmo> guizmos) {
    for (int i = 0; i < guizmos.Count; i++) {
      unsafe {
        var color = guizmos[i].Color;
        _bufferObject->ModelMatrix = guizmos[i].Transform.Matrix4;
        _bufferObject->GuizmoType = (int)guizmos[i].GuizmoType;
        _bufferObject->ColorX = color.X;
        _bufferObject->ColorY = color.Y;
        _bufferObject->ColorZ = color.Z;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<GuizmoBufferObject>(),
          _bufferObject
        );
      }

      vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
    }
  }

  public override unsafe void Dispose() {
    MemoryUtils.FreeIntPtr<GuizmoBufferObject>((nint)_bufferObject);

    base.Dispose();
  }
}
