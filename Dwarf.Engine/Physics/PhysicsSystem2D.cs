using Dwarf.EntityComponentSystem;
using Dwarf.Physics.Backends;
using Dwarf.Physics.Backends.Hammer;

namespace Dwarf.Physics;

public class PhysicsSystem2D : IDisposable {
  public IPhysicsProgram PhysicsProgram { get; private set; }

  public PhysicsSystem2D(BackendKind backendKind) {
    PhysicsProgram = backendKind switch {
      BackendKind.Hammer => new HammerProgram(),
      _ => new HammerProgram()
    };
  }

  public void Init(Span<Entity> entities) {

  }

  public void Tick() {

  }

  public void Dispose() {

  }
}