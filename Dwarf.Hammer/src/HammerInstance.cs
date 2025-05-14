using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerInstance {
  public HammerInterface HammerInterface { get; set; }
  public HammerWorld HammerWorld { get; set; }

  public delegate void ContactHandler(in BodyId body1, in BodyId body2);
  public ContactHandler? OnContactAdded;
  public ContactHandler? OnContactPersisted;
  public ContactHandler? OnContactExit;

  public HammerInstance() {
    HammerWorld = new HammerWorld(this);
    HammerInterface = new HammerInterface(HammerWorld);
  }
}
