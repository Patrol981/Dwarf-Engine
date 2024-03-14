using Dwarf.Engine.EntityComponentSystem;

namespace Dwarf.Engine;
public interface IRenderSystem : IDisposable {
  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures);
  public void Render(FrameInfo frameInfo, Span<Entity> entities);
}
