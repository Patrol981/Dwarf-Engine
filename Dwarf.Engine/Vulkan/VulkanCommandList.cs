using Dwarf.Engine.AbstractionLayer;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Vulkan;
public class VulkanCommandList : CommandList {
  public override void BindVertex(
    nint commandBuffer,
    uint meshIndex,
    DwarfBuffer[] vertexBuffers,
    ulong[] vertexOffsets
  ) {
    VkBuffer[] vBuffers = [vertexBuffers[meshIndex].GetBuffer()];
    unsafe {
      fixed (VkBuffer* buffersPtr = vBuffers)
      fixed (ulong* offsetsPtr = vertexOffsets) {
        vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
      }
    }
  }

  public override void BindIndex(nint commandBuffer, uint meshIndex, DwarfBuffer[] indexBuffers) {
    vkCmdBindIndexBuffer(commandBuffer, indexBuffers[meshIndex].GetBuffer(), 0, VkIndexType.Uint32);
  }

  public override void Draw(
    nint commandBuffer,
    uint meshIndex,
    ulong[] vertexCount,
    uint instanceCount,
    uint firstVertex,
    uint firstInstance
  ) {
    vkCmdDraw(commandBuffer, (uint)vertexCount[meshIndex], instanceCount, firstVertex, firstInstance);
  }

  public override void DrawIndexed(
    nint commandBuffer,
    uint meshIndex,
    ulong[] indexCount,
    uint instanceCount,
    uint firstIndex,
    int vertexOffset,
    uint firstInstance
  ) {
    vkCmdDrawIndexed(commandBuffer, (uint)indexCount[meshIndex], instanceCount, firstIndex, vertexOffset, firstInstance);
  }

  public override void SetViewport(
    nint commandBuffer,
    float x, float y,
    float width, float height,
    float minDepth, float maxDepth
  ) {
    var viewport = VkUtils.Viewport(x, y, width, height, minDepth, maxDepth);
    vkCmdSetViewport(commandBuffer, viewport);
  }
}
