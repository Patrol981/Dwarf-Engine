namespace Dwarf.AbstractionLayer;

public interface IDescriptorPool : IDisposable {
  public ulong GetHandle();
}