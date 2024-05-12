using Dwarf.EntityComponentSystem;

namespace Dwarf.Rendering;
public interface IRender3DElement : IDrawable {
  int MeshsesCount { get; }
  Mesh[] Meshes { get; }
  bool FinishedInitialization { get; }
  bool IsSkinned { get; }
  Entity GetOwner();
  Guid GetTextureIdReference(int index = 0);
  float CalculateHeightOfAnModel();
  // public void DrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, ulong offset, uint drawCount, uint stride);
}
