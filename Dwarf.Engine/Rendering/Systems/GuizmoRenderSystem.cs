using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Engine;
using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Rendering;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Systems;
public class GuizmoRenderSystem : SystemBase {
  private readonly unsafe GuizmoBufferObject* _bufferObject =
    (GuizmoBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<GuizmoBufferObject>());

  public GuizmoRenderSystem(
    IDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    CreatePipelineLayout<GuizmoBufferObject>(descriptorSetLayouts);
    CreatePipeline(
      renderer.GetSwapchainRenderPass(),
      "guizmo_vertex",
      "guizmo_fragment",
      new GuizmoPipelineProvider()
   );
  }

  public void Render(FrameInfo frameInfo) {
    _pipeline.Bind(frameInfo.CommandBuffer);
    unsafe {
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
    }

    var guizmos = Guizmos.Data;
    var perFrameGuizmos = Guizmos.PerFrameGuizmos;

    Draw(frameInfo, guizmos);

    if (perFrameGuizmos != null && perFrameGuizmos.Length > 0) {
      Draw(frameInfo, perFrameGuizmos);
      Guizmos.Free();
    }
  }

  private void Draw(FrameInfo frameInfo, Span<Guizmo> guizmos) {
    for (int i = 0; i < guizmos.Length; i++) {
      unsafe {
        var color = guizmos[i].Color;
        _bufferObject->ModelMatrix = guizmos[i].Transform.Matrix4;
        _bufferObject->GuizmoType = (int)guizmos[i].GuizmoType;
        _bufferObject->ColorX = color.X;
        _bufferObject->ColorY = color.Y;
        _bufferObject->ColorZ = color.Z;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          _pipelineLayout,
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
    MemoryUtils.FreeIntPtr((nint)_bufferObject);

    base.Dispose();
  }
}