using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Hammer;

namespace Dwarf.Physics.Backends.Hammer;

public class HammerProgram : IPhysicsProgram {
  private readonly HammerInstance _hammerInstance = null!;
  public Dictionary<Entity, HammerBodyWrapper> Bodies = [];
  public HammerInterface HammerInterface => _hammerInstance.HammerInterface;
  public float DeltaTime = 1.0f / 600.0f;

  public HammerProgram() {
    _hammerInstance = new();
  }

  public void Init(Span<Entity> entities) {
    foreach (var entity in entities) {
      var wrapper = new HammerBodyWrapper(HammerInterface);
      Bodies.Add(entity, wrapper);
      entity.GetComponent<Rigidbody2D>()?.Init(wrapper);
    }

    HammerInterface.SetGravity(0.01f);
  }

  public void Update() {
    _hammerInstance.HammerWorld.Simulate(DeltaTime);
  }

  public void Dispose() {

  }
}