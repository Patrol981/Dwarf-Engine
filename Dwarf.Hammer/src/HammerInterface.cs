using System.Numerics;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerInterface {
  private readonly HammerWorld _hammerWorld;

  public HammerInterface(HammerWorld hammerWorld) {
    _hammerWorld = hammerWorld;
  }

  public Vector2 GetPosition(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].Position;
  }

  public void SetPosition(in BodyId bodyId, in Vector2 position) {
    _hammerWorld.Bodies[bodyId].Position = position;
  }

  public BodyId CreateAndAddBody(Vector2 position) {
    return _hammerWorld.AddBody(position);
  }
}