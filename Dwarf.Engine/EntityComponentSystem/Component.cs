namespace Dwarf.EntityComponentSystem;

public abstract class Component {
  public Entity Owner { get; internal set; } = null!;
  public Guid ComponentId { get; init; } = Guid.NewGuid();
}