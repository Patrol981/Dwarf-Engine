using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Structs;

namespace Dwarf.Hammer.Models;

public class ShapeSettings {
  public Mesh Mesh { get; init; }
  public object? UserData { get; set; }
  public ObjectType ObjectType { get; set; }

  public ShapeSettings(Mesh mesh, object userData, ObjectType objectType) {
    Mesh = mesh;
    UserData = userData;
    ObjectType = objectType;
  }
}