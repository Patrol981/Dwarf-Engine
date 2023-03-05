using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Vulkan;

namespace Dwarf.Engine;

public abstract class Scene {
  protected readonly Device _device = null!;
  protected readonly TextureManager _textureManager = null!;
  private List<Entity> _entities = new();
  private List<List<string>> _texturePaths = new();

  public Scene(Device device, TextureManager textureManager) {
    _device = device;
    _textureManager = textureManager;
  }

  public virtual void LoadEntities() { }
  public virtual void LoadTextures() { }

  public void AddEntity(Entity entity) {
    _entities.Add(entity);
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