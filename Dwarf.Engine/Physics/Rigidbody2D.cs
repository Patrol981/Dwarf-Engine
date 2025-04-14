using Dwarf.EntityComponentSystem;

namespace Dwarf.Physics;

public class Rigidbody2D : Component, IDisposable {
  private readonly Application _app;

  public Rigidbody2D() {
    _app = Application.Instance;
  }

  public Rigidbody2D(Application app) {
    _app = app;
  }

  public void Dispose() {
    GC.SuppressFinalize(this);
  }
}