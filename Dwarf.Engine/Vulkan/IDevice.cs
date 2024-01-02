namespace Dwarf.Engine;

public interface IDevice {
  public void CreateBuffer(ulong size, BufferUsage usageFlags);
}