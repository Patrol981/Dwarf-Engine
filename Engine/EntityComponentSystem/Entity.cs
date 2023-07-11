using Dwarf.Engine.Rendering;
using Dwarf.Engine.Rendering.UI;

namespace Dwarf.Engine.EntityComponentSystem;

public class Entity {
  public bool CanBeDisposed = false;

  private ComponentManager _componentManager;
  private string _name = "Entity";
  private Guid _guid = Guid.NewGuid();
  private bool _isActive = true;

  public Entity() {
    _componentManager = new ComponentManager();
  }

  public void AddComponent(Component component) {
    component.Owner = this;
    _componentManager.AddComponent(component);
  }

  public T GetComponent<T>() where T : Component, new() {
    return _componentManager.GetComponent<T>();
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

  public static ReadOnlySpan<Entity> Distinct<T>(List<Entity> entities) where T : Component {
    return entities.Where(e => e.HasComponent<T>()).ToArray();

    /*
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Count; i++) {
      if (entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities;
    */
  }

  public static ReadOnlySpan<Entity> Distinct<T>(ReadOnlySpan<Entity> entities) where T : Component {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static List<Entity> DistinctList<T>(List<Entity> entities) where T : Component {
    return entities.Where(e => e.HasComponent<T>()).ToList();
  }

  public static ReadOnlySpan<Entity> DistinctInterface<T>(List<Entity> entities) where T : IDrawable {
    return entities.Where(e => e.IsDrawable<T>()).ToArray();
  }

  public static ReadOnlySpan<Entity> DistinctInterface<T>(ReadOnlySpan<Entity> entities) where T : IDrawable {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      // if (entities[i] is IUIElement) returnEntities.Add(entities[i]);
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
}