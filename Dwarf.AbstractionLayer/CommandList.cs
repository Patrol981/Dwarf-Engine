namespace Dwarf.AbstractionLayer;
public abstract class CommandList {
  public abstract void BindVertex(
    IntPtr commandBuffer,
    uint index,
    DwarfBuffer[] vertexBuffers,
    ulong[] vertexOffsets
  );

  public abstract void BindIndex(IntPtr commandBuffer, uint index, DwarfBuffer[] indexBuffers);
  public abstract void Draw(
    nint commandBuffer,
    uint meshIndex,
    ulong[] vertexCount,
    uint instanceCount,
    uint firstVertex,
    uint firstInstance
  );
  public abstract void DrawIndexed(
    nint commandBuffer,
    uint meshIndex,
    ulong[] indexCount,
    uint instanceCount,
    uint firstIndex,
    int vertexOffset,
    uint firstInstance
  );

  public abstract void SetViewport(
    nint commandBuffer,
    float x, float y,
    float width, float height,
    float minDepth, float maxDepth
  );
}
