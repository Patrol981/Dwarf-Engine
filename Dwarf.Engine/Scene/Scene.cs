using Dwarf.EntityComponentSystem;

namespace Dwarf;

public abstract class Scene {
  protected readonly Application _app;
  private readonly List<Entity> _entities = new();
  private List<List<string>> _texturePaths = new();

  public Scene(Application app) {
    _app = app;
  }

  public virtual void LoadEntities() { }
  public virtual void LoadTextures() { }
  public virtual void LoadFonts() { }

  public void AddEntity(Entity entity) {
    _entities.Add(entity);
  }

  public void AddEntities(Entity[] entities) {
    _entities.AddRange(entities);
  }

  public List<Entity> GetEntities() {
    return _entities;
  }

  public Entity GetEntity(int index) => _entities[index];

  public void RemoveEntityAt(int index) {
    _entities.RemoveAt(index);
  }

  public void RemoveEntity(Entity entity) {
    _entities.Remove(entity);
  }

  public void DestroyEntity(Entity entity) {
    entity.CanBeDisposed = true;
  }

  public void RemoveEntityRange(int index, int count) {
    _entities.RemoveRange(index, count);
  }

  public void SetTexturePaths(List<List<string>> paths) {
    _texturePaths = paths;
  }

  public List<List<string>> GetTexturePaths() {
    return _texturePaths;
  }
}