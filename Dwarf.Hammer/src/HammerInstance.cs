namespace Dwarf.Hammer;

public class HammerInstance {
  public HammerInterface HammerInterface { get; set; }
  public HammerWorld HammerWorld { get; set; }

  public HammerInstance() {
    HammerWorld = new HammerWorld();
    HammerInterface = new HammerInterface(HammerWorld);
  }
}
