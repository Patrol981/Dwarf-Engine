using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering;
public interface IRender3DElement : IDrawable {
  int MeshsesCount { get; }
  Mesh[] Meshes { get; }
  bool FinishedInitialization { get; }
  bool IsSkinned { get; }
  DwarfBuffer Ssbo { get; }
  Matrix4x4[] InverseMatrices { get; }
  VkDescriptorSet SkinDescriptor { get; }
  void BuildDescriptors(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool);
  Entity GetOwner();
  Guid GetTextureIdReference(int index = 0);
  float CalculateHeightOfAnModel();
  // public void DrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, ulong offset, uint drawCount, uint stride);
}
