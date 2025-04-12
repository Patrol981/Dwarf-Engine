using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Vulkan;

namespace Dwarf.Rendering.Renderer2D;

public interface IDrawable2D : IDrawable {
  void BuildDescriptors(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool);

  Entity Entity { get; }
  bool Active { get; }
  ITexture Texture { get; }
}