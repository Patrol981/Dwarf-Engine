using Dwarf.Model.Animation;
using Dwarf.Physics;
using Dwarf.Rendering;
namespace Dwarf.EntityComponentSystem;

public class Entity {
  public bool CanBeDisposed = false;
  public bool Collected = false;
  public EntityLayer Layer = EntityLayer.Default;
  public bool IsImportant = false;

  private readonly ComponentManager _componentManager;
  private readonly object _componentLock = new object();

  public Entity() {
    EntityID = Guid.NewGuid();
    _componentManager = new ComponentManager();
  }

  public Entity(Guid entityId) {
    EntityID = entityId;
    _componentManager = new ComponentManager();
  }

  public void AddComponent(Component component) {
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    component.Owner = this;
    _componentManager.AddComponent(component);
  }

  public T GetComponent<T>() where T : Component, new() {
    lock (_componentLock) {
      if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
      return _componentManager.GetComponent<T>();
    }
  }

  public T? TryGetComponent<T>() where T : Component, new() {
    if (CanBeDisposed) return null;
    return HasComponent<T>() ? GetComponent<T>() : null;
  }

  public T GetScript<T>() where T : DwarfScript {
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    return _componentManager.GetComponent<T>();
  }

  public DwarfScript[] GetScripts() {
    lock (_componentLock) {
      if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");

      var components = _componentManager.GetAllComponents();
      var list = new List<DwarfScript>();

      foreach (var item in components) {
        var t = typeof(DwarfScript).IsAssignableFrom(item.Key);
        if (t) {
          var value = item.Value;
          list.Add((DwarfScript)value);
        }
      }

      return [.. list];
    }
  }

  public Component GetDrawable<T>() where T : IDrawable {
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
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
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var components = _componentManager.GetAllComponents();

    var list = new List<Component>();

    foreach (var component in components) {
      var t = typeof(T).IsAssignableFrom(component.Key);
      if (t) {
        var value = component.Value;
        list.Add(value);
      }
    }

    return [.. list];
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

    return [.. list];
  }

  public bool HasComponent<T>() where T : Component {
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    return _componentManager.GetComponent<T>() != null;
  }

  public bool IsDrawable<T>() where T : IDrawable {
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var d = GetDrawable<T>();
    return d != null;
  }

  public void RemoveComponent<T>() where T : Component {
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    _componentManager.RemoveComponent<T>();
  }

  public ComponentManager GetComponentManager() {
    return _componentManager;
  }

  public static T? FindComponentOfType<T>() where T : Component, new() {
    var entities = Application.Instance.GetEntities();
    var target = entities.Where(x => x.HasComponent<T>() && !x.CanBeDisposed)
      .FirstOrDefault();
    return target == null ? null : target.GetComponent<T>();
  }

  public static T? FindComponentByName<T>(string name) where T : Component, new() {
    var entities = Application.Instance.GetEntities();
    var target = entities.Where(x => x.Name == name && !x.CanBeDisposed)
      .FirstOrDefault();
    return target == null ? null : target.GetComponent<T>();
  }

  public static Entity? FindEntityByName(string name) {
    var entities = Application.Instance.GetEntities();
    var target = entities.Where(x => x.Name == name && !x.CanBeDisposed)
      .FirstOrDefault();
    return target ?? null!;
  }

  public Entity Clone() {
    if (CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");

    var clone = new Entity() {
      Name = $"{Name} [CLONE]"
    };

    var transform = TryGetComponent<Transform>();
    var material = TryGetComponent<MaterialComponent>();
    var model = TryGetComponent<MeshRenderer>();
    var rigidbody = TryGetComponent<Rigidbody>();

    if (transform != null) {
      clone.AddTransform(transform.Position, transform.Rotation, transform.Scale);
    }
    if (material != null) {
      clone.AddMaterial(material.Color);
    }
    if (model != null) {
      clone.AddComponent(EntityCreator.CopyModel(in model));
      clone.AddComponent(new AnimationController());
      clone.GetComponent<AnimationController>().Init(clone.GetComponent<MeshRenderer>());
    }
    if (rigidbody != null) {
      clone.AddRigidbody(
        rigidbody.PrimitiveType,
        rigidbody.Size,
        rigidbody.Offset,
        MotionType.Dynamic,
        rigidbody.Flipped
      );
      Application.Instance.Systems.PhysicsSystem.Init([clone]);
    }

    return clone;
  }

  public bool Active { get; set; } = true;

  public string Name { get; set; } = "Entity";

  public Guid EntityID { get; set; }

  internal class Comparer : IComparer<Entity> {
    public int Compare(Entity? x, Entity? y) {
      return x?.EntityID < y?.EntityID ? -1 : x?.EntityID > y?.EntityID ? 1 : 0;
    }
  }
}