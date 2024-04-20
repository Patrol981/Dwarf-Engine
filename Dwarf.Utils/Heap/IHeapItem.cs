namespace Dwarf.Utils;

public interface IHeapItem<T> : IComparable<T> {
  int HeapIndex { get; set; }
}