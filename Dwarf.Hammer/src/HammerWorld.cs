using System.Numerics;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerWorld {
  internal Dictionary<BodyId, HammerObject> Bodies = [];
  internal float Gravity;

  public void Simulate(float dt) {
    var sprites = Bodies.Where(x => x.Value.ObjectType == Enums.ObjectType.Sprite).ToArray();
    foreach (var sprite1 in sprites) {
      foreach (var sprite2 in sprites) {
        if (sprite1.Key.Id == sprite2.Key.Id) continue;
        var isColl = sprite1.Value.AABB.CheckCollision(sprite1.Value.Position, sprite2.Value.Position, sprite2.Value.AABB);
        if (isColl) {
          var dotProduct = Vector2.Dot(sprite1.Value.Velocity, sprite2.Value.Position);

          if (dotProduct > 0) {
            sprite1.Value.Velocity.X = 0;
            sprite1.Value.Velocity.Y = 0;
          }
        }
      }
    }

    foreach (var body in Bodies.Values) {
      if (body.MotionType == Enums.MotionType.Dynamic) {
        body.Position.Y += dt * Gravity;
      }

      if (body.MotionType != Enums.MotionType.Static) {
        var x = body.Velocity.X * dt;
        var y = body.Velocity.Y * dt;

        body.Velocity.X -= x;
        body.Velocity.Y -= y;

        body.Position.X += x;
        body.Position.Y += y;
      }
    }
  }

  internal BodyId AddBody(Vector2 position) {
    var bodyId = new BodyId();
    var hammerObject = new HammerObject {
      Position = position
    };
    Bodies.Add(bodyId, hammerObject);
    return bodyId;
  }

  internal void RemoveBody() {

  }
}