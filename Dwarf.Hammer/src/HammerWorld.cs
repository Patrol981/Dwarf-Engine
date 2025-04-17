using System.Numerics;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerWorld {
  internal Dictionary<BodyId, HammerObject> Bodies = [];
  internal float Gravity;

  public void Simulate(float dt) {
    foreach (var body in Bodies.Values) {
      if (body.MotionType != Enums.MotionType.Static) {
        body.Position.Y += dt * Gravity;
      }
    }
  }

  public BodyId AddBody(Vector2 position) {
    var bodyId = new BodyId();
    var hammerObject = new HammerObject {
      Position = position
    };
    Bodies.Add(bodyId, hammerObject);
    return bodyId;
  }

  public void RemoveBody() {

  }
}