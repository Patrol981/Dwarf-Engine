using System.Numerics;

using Dwarf.Engine.Rendering;

namespace Dwarf.Engine.Globals;
public static class Guizmos {
  private static readonly List<Guizmo> s_guizmos = [];

  public static void AddCircular(Vector3 position = default, Vector3 scale = default) {
    var guizmo = new Guizmo(GuizmoType.Circular, position, scale);
    s_guizmos.Add(guizmo);
  }

  public static void AddCube(Vector3 position = default, Vector3 scale = default) {
    var guizmo = new Guizmo(GuizmoType.Cubic, position, scale);
    s_guizmos.Add(guizmo);
  }

  public static void RemoveGuizmo(Guid id) {
    var target = s_guizmos.Where(x => x.Id == id).FirstOrDefault();
    if (target == null) return;
    s_guizmos.Remove(target);
  }

  public static Span<Guizmo> Data => s_guizmos.ToArray();
}
