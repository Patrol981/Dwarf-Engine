using Dwarf.EntityComponentSystem;
using Dwarf.Hammer;

namespace Dwarf.Physics.Backends.Hammer;

public class HammerProgram : IPhysicsProgram {
  private readonly HammerInstance _hammerInstance = null!;
  public Dictionary<Entity, HammerBodyWrapper> Bodies = [];
  public HammerInterface HammerInterface => _hammerInstance.HammerInterface;

  public HammerProgram() {
    _hammerInstance = new();
  }

  public void Init(Span<Entity> entities) {
    // throw new NotImplementedException();
    foreach (var entity in entities) {
      var wrapper = new HammerBodyWrapper(HammerInterface);
      Bodies.Add(entity, wrapper);
      entity.GetComponent<Rigidbody2D>()?.Init(wrapper);
    }
  }

  public void Update() {
    _hammerInstance.HammerWorld.Simulate();
  }

  public void Dispose() {

  }
}