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

  public bool HasComponent<T>() where T : Component {
    return _componentManager.GetComponent<T>() != null;
  }

  public void RemoveComponent<T>() where T : Component {
    _componentManager.RemoveComponent<T>();
  }

  public ComponentManager GetComponentManager() {
    return _componentManager;
  }

  public static List<Entity> Distinct<T>(List<Entity> entities) where T : Component {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Count; i++) {
      if (entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities;
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