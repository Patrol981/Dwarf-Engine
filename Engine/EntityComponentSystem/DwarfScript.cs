using Dwarf.Extensions.Logging;

namespace Dwarf.Engine.EntityComponentSystem;
public class DwarfScript : Component {
  protected bool DidAwake { get; private set; }
  protected bool DidStart { get; private set; }

  public virtual void Start() {
    if (DidStart) return;
    DidStart = true;
  }
  public virtual void Awake() {
    if (DidAwake) return;
    DidAwake = true;
  }
  public virtual void Update() { }
  public virtual void FixedUpdate() { }

  public virtual void CollisionEnter(Entity entity) { }

  public virtual void CollisionStay(Entity entity) { }

  public virtual void CollisionExit(Entity entity) { }
}
