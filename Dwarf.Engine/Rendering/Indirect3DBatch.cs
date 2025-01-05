using Dwarf;
using Dwarf.Model;

namespace Dwarf.Rendering;

public class Indirect3DBatch {
  public List<KeyValuePair<Node, ObjectData>> NodeObjects = [];
  public string? Name { get; set; }
  public uint Count { get; set; }
}