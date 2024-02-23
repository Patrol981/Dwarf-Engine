using Dwarf.Vulkan;

namespace Dwarf.Engine.AbstractionLayer;
public abstract class CommandList {
  public abstract Task Bind(ulong commandBuffer, uint index, DwarfBuffer[] buffers);
  public abstract Task Draw();
}
