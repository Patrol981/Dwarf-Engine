namespace Dwarf.Engine.EntityComponentSystem;

public class ComponentManager {
  private Dictionary<Type, Component> _components;

  public ComponentManager() {
    _components = new Dictionary<Type, Component>();
  }

  public void AddComponent(Component component) {
    _components[component.GetType()] = component;
  }

  public T GetComponent<T>() where T : Component {
    var component = _components!.TryGetValue(typeof(T), out var value);
    return component ? (T)value! : null;
  }

  public void RemoveComponent<T>() where T : Component {
    _components.Remove(typeof(T));
  }

  public Dictionary<Type, Component> GetAllComponents() {
    return _components;
  }
}