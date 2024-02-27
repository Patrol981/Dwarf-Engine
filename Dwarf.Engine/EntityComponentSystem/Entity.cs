using Dwarf.Engine.Rendering;
namespace Dwarf.Engine.EntityComponentSystem;

public class Entity {
  public bool CanBeDisposed = false;

  private ComponentManager _componentManager;
  private string _name = "Entity";
  private Guid _guid;
  private bool _isActive = true;

  private readonly object _componentLock = new object();

  public Entity() {
    _guid = Guid.NewGuid();
    _componentManager = new ComponentManager();
  }

  public Entity(Guid entityId) {
    _guid = entityId;
    _componentManager = new ComponentManager();
  }

  public void AddComponent(Component component) {
    component.Owner = this;
    _componentManager.AddComponent(component);
  }

  public T GetComponent<T>() where T : Component, new() {
    lock (_componentLock) {
      return _componentManager.GetComponent<T>();
    }
  }

  public T? TryGetComponent<T>() where T : Component, new() {
    if (HasComponent<T>()) {
      return GetComponent<T>();
    }
    return null!;
  }

  public T GetScript<T>() where T : DwarfScript {
    return _componentManager.GetComponent<T>();
  }

  public DwarfScript[] GetScripts() {
    lock (_componentLock) {
      var components = _componentManager.GetAllComponents();
      var list = new List<DwarfScript>();

      foreach (var item in components) {
        var t = typeof(DwarfScript).IsAssignableFrom(item.Key);
        if (t) {
          var value = item.Value;
          list.Add((DwarfScript)value);
        }
      }

      return list.ToArray();
    }
  }

  public Component GetDrawable<T>() where T : IDrawable {
    var components = _componentManager.GetAllComponents();

    foreach (var component in components) {
      var t = typeof(T).IsAssignableFrom(component.Key);
      if (t) {
        var value = component.Value;
        return value;
      }
    }
    return null!;
  }

  public Component[] GetDrawables<T>() where T : IDrawable {
    var components = _componentManager.GetAllComponents();

    var list = new List<Component>();

    foreach (var component in components) {
      var t = typeof(T).IsAssignableFrom(component.Key);
      if (t) {
        var value = component.Value;
        list.Add(value);
      }
    }

    return list.ToArray();
  }

  public void DisposeEverything() {
    var components = GetDisposables();
    foreach (var comp in components) {
      var target = comp as IDisposable;
      target?.Dispose();
    }
  }

  public Component[] GetDisposables() {
    var components = _componentManager.GetAllComponents();
    var list = new List<Component>();

    foreach (var component in components) {
      var t = typeof(IDisposable).IsAssignableFrom(component.Key);
      if (t) {
        var value = component.Value;
        list.Add(value);
      }
    }

    return list.ToArray();
  }

  public bool HasComponent<T>() where T : Component {
    return _componentManager.GetComponent<T>() != null;
  }

  public bool IsDrawable<T>() where T : IDrawable {
    var d = GetDrawable<T>();
    return d != null;
  }

  public void RemoveComponent<T>() where T : Component {
    _componentManager.RemoveComponent<T>();
  }

  public ComponentManager GetComponentManager() {
    return _componentManager;
  }

  public static ReadOnlySpan<DwarfScript> GetScripts(List<Entity> entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities) {
      list.AddRange(e.GetScripts().Where(x => x.Owner!.CanBeDisposed == false));
    }

    return list.ToArray();
  }

  public static ReadOnlySpan<DwarfScript> GetScripts(Entity[] entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities) {
      list.AddRange(e.GetScripts().Where(x => x.Owner!.CanBeDisposed == false));
    }

    return list.ToArray();
  }

  public static ReadOnlySpan<Entity> Distinct<T>(List<Entity> entities) where T : Component {
    return entities.Where(e => e.HasComponent<T>()).ToArray();
  }

  public static ReadOnlySpan<Entity> Distinct<T>(ReadOnlySpan<Entity> entities) where T : Component {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static Span<Entity> DistinctList<T>(List<Entity> entities) where T : Component {
    return entities.Where(e => e.HasComponent<T>()).ToArray();
  }

  public static Span<Entity> DistinctInterface<T>(List<Entity> entities) where T : IDrawable {
    return entities.Where(e => e.IsDrawable<T>()).ToArray();
  }

  public static ReadOnlySpan<Entity> DistinctInterface<T>(ReadOnlySpan<Entity> entities) where T : IDrawable {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].IsDrawable<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public bool Active {
    get { return _isActive; }
    set { _isActive = value; }
  }

  public string Name {
    get { return _name; }
    set { _name = value; }
  }

  public Guid EntityID {
    get { return _guid; }
    set { _guid = value; }
  }

  internal class Comparer : IComparer<Entity> {
    public int Compare(Entity? x, Entity? y) {
      if (x?.EntityID < y?.EntityID)
        return -1;
      else if (x?.EntityID > y?.EntityID)
        return 1;
      else
        return 0;
    }
  }
}