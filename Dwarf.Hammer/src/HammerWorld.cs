using System.Numerics;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerWorld {
  internal Dictionary<BodyId, HammerObject> Bodies = [];
  internal float Gravity = 9.80665f;

  public void Simulate(float dt) {
    var sprites = Bodies.Where(x => x.Value.ObjectType == Enums.ObjectType.Sprite).ToArray();
    var tilemaps = Bodies.Where(x => x.Value.ObjectType == Enums.ObjectType.Tilemap).ToArray();

    foreach (var sprite1 in sprites) {
      var collidesWithAnythingGround = false;
      foreach (var sprite2 in sprites) {
        if (sprite1.Key.Id == sprite2.Key.Id) continue;
        var isColl = sprite1.Value.AABB.CheckCollision(sprite1.Value.Position, sprite2.Value.Position, sprite2.Value.AABB);
        if (isColl) {
          var dotProduct = Vector2.Dot(sprite1.Value.Velocity, sprite2.Value.Position);

          sprite1.Value.Position.X -= dotProduct;
          sprite1.Value.Velocity.Y = 0;

          // sprite2.Value.Position.X += sprite1.Value.Velocity.X * dt;
          // sprite1.Value.Position.Y -= dotProduct;
        }
      }

      float spriteMinX = sprite1.Value.Position.X;
      float spriteMaxX = spriteMinX + sprite1.Value.AABB.Width;
      float spriteMinY = sprite1.Value.Position.Y;
      float spriteMaxY = spriteMinY + sprite1.Value.AABB.Height;

      foreach (var tilemap in tilemaps) {
        foreach (var aabb in tilemap.Value.TilemapAABBs) {
          // var isColl = AABB.CheckCollisionWithTilemap(tilemap.Value, aabb, spriteMinX, spriteMaxX, spriteMinY, spriteMaxY);
          var isColl = AABB.CheckCollisionWithTilemapMTV(sprite1.Value.AABB, sprite1.Value.Position, aabb, tilemap.Value.Position, out var mtv);
          // var isColl = aabb.CheckCollision(tilemap.Value.Position, sprite1.Value.Position, aabb);
          if (isColl) {
            var dotProduct = Vector2.Dot(sprite1.Value.Velocity, sprite1.Value.Position);

            // sprite1.Value.Velocity.X -= dotProduct;

            // if (sprite1.Value.Velocity.Y > 0) {
            //   sprite1.Value.Velocity.Y = 0;
            // }

            mtv.Y *= -1;
            sprite1.Value.Position += mtv;
            sprite1.Value.Velocity = Vector2.Zero;

            collidesWithAnythingGround = true;
          }
        }

        // var isColl = AABB.CollideAndResolve(sprite1.Value, tilemap.Value.Edges);
        // foreach (var aabb in tilemap.Value.TilemapAABBs) {
        //   var isColl = sprite1.Value.AABB.CheckCollision(sprite1.Value.Position, tilemap.Value.Position, aabb);
        //   if (isColl) {
        //     var dotProduct = Vector2.Dot(sprite1.Value.Velocity, tilemap.Value.Position);

        //     // sprite1.Value.Velocity.X = 0;
        //     // sprite1.Value.Velocity.Y = 0;

        //     sprite1.Value.Velocity.X = 0;
        //     sprite1.Value.Velocity.Y = 0;

        //     // Console.WriteLine("coll");
        //   }
        // }
      }
      if (collidesWithAnythingGround) {
        sprite1.Value.Grounded = true;
      } else {
        sprite1.Value.Grounded = false;
      }
    }

    foreach (var body in Bodies) {
      if (body.Value.MotionType == Enums.MotionType.Dynamic) {
        if (!body.Value.Grounded) {
          if (body.Value.Force.Y < 0) {
            body.Value.Velocity.Y -= dt * Gravity;
            body.Value.Force.Y += dt * Gravity;
          } else {
            body.Value.Velocity.Y += dt * Gravity;
          }

        }


        var f = dt * body.Value.Force.Y;
        // body.Value.Velocity.Y -= f * Gravity;
        // body.Value.Force.Y += f;
      }

      if (body.Value.MotionType != Enums.MotionType.Static) {
        var x = body.Value.Velocity.X * dt;
        var y = body.Value.Velocity.Y * dt;

        body.Value.Velocity.X -= x;
        body.Value.Velocity.Y -= y;

        body.Value.Position.X += x;
        body.Value.Position.Y += y;
      }

      // foreach (var body2 in Bodies) {
      //   if (body2.Key.Id == body.Key.Id) continue;

      //   if (body.Value.ObjectType == Enums.ObjectType.Tilemap) {
      //     var isColl = AABB.CollideAndResolve(body2.Value, body.Value.Edges);
      //     if (isColl) {
      //       // Console.WriteLine(isColl);
      //     }
      //   }
      // }
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