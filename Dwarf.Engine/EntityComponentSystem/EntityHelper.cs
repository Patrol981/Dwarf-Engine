using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.EntityComponentSystem;
public static class EntityHelper {
  public static Entity[] Distinct<T>(this List<Entity> entities) where T : Component {
    return entities.Where(e => !e.CanBeDisposed && e.HasComponent<T>()).ToArray();
  }
  public static ReadOnlySpan<Entity> DistinctAsReadOnlySpan<T>(this List<Entity> entities) where T : Component {
    return entities.Where(e => !e.CanBeDisposed && e.HasComponent<T>()).ToArray();
  }

  public static Span<Entity> DistinctAsSpan<T>(this List<Entity> entities) where T : Component {
    return entities.Where(e => !e.CanBeDisposed && e.HasComponent<T>()).ToArray();
  }

  public static ReadOnlySpan<Entity> DistinctReadOnlySpan<T>(this ReadOnlySpan<Entity> entities) where T : Component {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (!entities[i].CanBeDisposed && entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static ReadOnlySpan<Entity> Distinct<T>(this Entity[] entities) where T : Component {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (!entities[i].CanBeDisposed && entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static Span<Entity> DistinctInterface<T>(this List<Entity> entities) where T : IDrawable {
    return entities.Where(e => !e.CanBeDisposed && e.IsDrawable<T>()).ToArray();
  }

  public static Span<Entity> DistinctInterface<T>(this Entity[] entities) where T : IDrawable {
    return entities.Where(e => !e.CanBeDisposed && e.IsDrawable<T>()).ToArray();
  }

  public static Span<IRender3DElement> DistinctI3D(this Entity[] entities) {
    var drawables3D = new List<IRender3DElement>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      if (entities[i].GetDrawable<IRender3DElement>() is IRender3DElement target) {
        drawables3D.Add(target);
      }
    }
    return drawables3D.ToArray();
  }

  public static Span<IDrawable2D> DistinctI2D(this Entity[] entities) {
    var drawables2D = new List<IDrawable2D>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      if (entities[i].GetDrawable<IDrawable2D>() is IDrawable2D target) {
        drawables2D.Add(target);
      }
    }

    return drawables2D.OrderBy(x => x.Entity.GetComponent<Transform>().Position.Z).ToArray();
  }

  public static ReadOnlySpan<Entity> DistinctInterface<T>(this ReadOnlySpan<Entity> entities) where T : IDrawable {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      if (entities[i].IsDrawable<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static ReadOnlySpan<DwarfScript> GetScripts(this List<Entity> entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities.Where(x => !x.CanBeDisposed)) {
      list.AddRange(e.GetScripts());
    }

    return list.ToArray();
  }

  public static ReadOnlySpan<DwarfScript> GetScriptsAsSpan(this Entity[] entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities.Where(x => !x.CanBeDisposed)) {
      list.AddRange(e.GetScripts());
    }

    return list.ToArray();
  }

  public static DwarfScript[] GetScriptsAsArray(this Entity[] entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities.Where(x => !x.CanBeDisposed)) {
      list.AddRange(e.GetScripts());
    }

    return [.. list];
  }
}
