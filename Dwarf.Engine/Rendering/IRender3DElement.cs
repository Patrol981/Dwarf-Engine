namespace Dwarf.Engine.Rendering;
public interface IRender3DElement : IDrawable {
  public int MeshsesCount { get; }
  public Mesh[] Meshes { get; }
  public bool FinishedInitialization { get; }
  public Guid GetTextureIdReference(int index = 0);
  public float CalculateHeightOfAnModel();
  // public void DrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, ulong offset, uint drawCount, uint stride);
}
