using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Hammer;

namespace Dwarf.Physics.Backends.Hammer;

public class HammerProgram : IPhysicsProgram {
  private readonly HammerInstance _hammerInstance = null!;
  public Dictionary<Entity, HammerBodyWrapper> Bodies = [];
  public HammerInterface HammerInterface => _hammerInstance.HammerInterface;
  public float DeltaTime = 1.0f / 60.0f;

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
    _hammerInstance.HammerWorld.Simulate(Time.DeltaTime);
  }

  public void Dispose() {

  }
}