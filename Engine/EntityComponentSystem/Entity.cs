namespace Dwarf.Engine.EntityComponentSystem;

public class Entity {
  public bool CanBeDisposed = false;

  private ComponentManager _componentManager;
  private string _name = "Entity";
  private Guid _guid = Guid.NewGuid();

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

  public void RemoveComponent<T>() where T : Component {
    _componentManager.RemoveComponent<T>();
  }

  public ComponentManager GetComponentManager() {
    return _componentManager;
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