using System.Numerics;

namespace Dwarf.Engine.Rendering;
public class Guizmo {
  public Guid Id { get; init; }
  public GuizmoType GuizmoType { get; init; }
  public Transform Transform { get; init; }

  public Guizmo(GuizmoType guizmoType) {
    GuizmoType = guizmoType;
    Id = Guid.NewGuid();
    Transform = new Transform();
  }

  public Guizmo(GuizmoType guizmoType, Vector3 position) {
    GuizmoType = guizmoType;
    Id = Guid.NewGuid();
    Transform = new Transform(position);
  }

  public Guizmo(GuizmoType guizmoType, Vector3 position, Vector3 scale) {
    GuizmoType = guizmoType;
    Id = Guid.NewGuid();
    Transform = new Transform(position, Vector3.Zero, scale);
  }
}
