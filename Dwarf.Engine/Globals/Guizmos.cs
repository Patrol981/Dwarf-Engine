using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Dwarf.Rendering;

namespace Dwarf.Globals;
public static class Guizmos {
  private static readonly List<Guizmo> s_guizmos = [];
  private static readonly List<Guizmo> s_perFrameGuizmos = [];

  public static Guizmo AddCircular(
    Vector3 position = default,
    Vector3 scale = default,
    Vector3 color = default
  ) {
    var guizmo = new Guizmo(GuizmoType.Circular, position, scale, color);
    s_guizmos.Add(guizmo);
    return guizmo;
  }

  public static Guizmo AddCube(
    Vector3 position = default,
    Vector3 scale = default,
    Vector3 color = default
  ) {
    var guizmo = new Guizmo(GuizmoType.Cubic, position, scale, color);
    s_guizmos.Add(guizmo);
    return guizmo;
  }

  [Experimental("Guizmos")]
  public static void DrawCircular(
    Vector3 position = default,
    Vector3 scale = default,
    Vector3 color = default
  ) {
    var guizmo = new Guizmo(GuizmoType.Circular, position, scale, color);
    s_perFrameGuizmos.Add(guizmo);
  }

  [Experimental("Guizmos")]
  public static void DrawCube(
    Vector3 position = default,
    Vector3 scale = default,
    Vector3 color = default
  ) {
    var guizmo = new Guizmo(GuizmoType.Cubic, position, scale, color);
    s_perFrameGuizmos.Add(guizmo);
  }

  public static void RemoveGuizmo(Guid id) {
    var target = s_guizmos.Where(x => x.Id == id).FirstOrDefault();
    if (target == null) return;
    s_guizmos.Remove(target);
  }

  public static void Free() {
    s_perFrameGuizmos?.Clear();
  }

  public static Span<Guizmo> Data => s_guizmos.ToArray();
  public static Span<Guizmo> PerFrameGuizmos => s_perFrameGuizmos.ToArray();
}
