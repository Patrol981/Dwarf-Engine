using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerWorld {
  internal Dictionary<BodyId, HammerObject> Bodies = [];
  internal float Gravity = 9.80665f;

  public void SimulateNew(float dt) {
    // 1) Pull out just the sprites & tilemaps
    var sprites = Bodies.Values
                         .Where(b => b.ObjectType == ObjectType.Sprite)
                         .ToArray();
    var tilemaps = Bodies.Values
                         .Where(b => b.ObjectType == ObjectType.Tilemap)
                         .ToArray();

    // 2) Apply gravity (once) and clear grounded
    foreach (var s in sprites) {
      s.Grounded = false;

      if (s.MotionType == MotionType.Dynamic && !s.Grounded)
        s.Velocity.Y += Gravity * dt;
    }

    // 3) For each sprite, do an X‐pass then a Y‐pass
    foreach (var s in sprites) {
      // --- HORIZONTAL ---
      s.Position.X += s.Velocity.X * dt;

      foreach (var tm in tilemaps)
        foreach (var tile in tm.TilemapAABBs) {
          if (!AABB.CheckCollisionWithTilemapMTV(
                  s.AABB, s.Position,
                  tile, tm.Position,
                  out var mtv))
            continue;

          // If MTV is primarily horizontal, resolve X
          if (Math.Abs(mtv.X) > Math.Abs(mtv.Y)) {
            s.Position.X += mtv.X;
            s.Velocity.X = 0;
          }
        }

      // --- VERTICAL ---
      s.Position.Y += s.Velocity.Y * dt;

      foreach (var tm in tilemaps)
        foreach (var tile in tm.TilemapAABBs) {
          if (!AABB.CheckCollisionWithTilemapMTV(
                  s.AABB, s.Position,
                  tile, tm.Position,
                  out var mtv))
            continue;

          // If MTV is primarily vertical, resolve Y
          if (Math.Abs(mtv.Y) >= Math.Abs(mtv.X)) {
            s.Position.Y += mtv.Y;
            s.Velocity.Y = 0;

            // Only floor‐collisions (mtv.Y < 0) make you grounded
            if (mtv.Y < 0)
              s.Grounded = true;
          }
        }
    }

    // 4) Finally, integrate any remaining bodies (if you have non‐sprite dynamics)
    foreach (var b in Bodies.Values) {
      if (b.MotionType != MotionType.Static)
        b.Position += b.Velocity * dt;
    }
  }

  public void Simulate_Old(float dt) {
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

            mtv.Y *= -1;
            sprite1.Value.Position += mtv;
            sprite1.Value.Velocity.Y = 0;

            collidesWithAnythingGround = true;
          }
        }
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

  public Task Simulate(float dt) {
    // await Task.Delay(TimeSpan.FromMilliseconds(dt));
    // Thread.Sleep(TimeSpan.FromMilliseconds(dt));

    var sprites = Bodies.Values.Where(x => x.ObjectType == Enums.ObjectType.Sprite).ToArray();

    HandleSprites(sprites);
    HandleGravity(dt);

    return Task.CompletedTask;
  }

  internal void HandleGravity(float dt) {
    foreach (var body in Bodies) {
      if (body.Value.MotionType == Enums.MotionType.Dynamic) {
        if (!body.Value.Grounded) {
          if (body.Value.Force.Y < 0) {
            body.Value.Velocity.Y -= dt * Gravity * body.Value.Mass;
            body.Value.Force.Y += dt * Gravity;
            // body.Value.Force.Y /= 2 * dt;
          } else {
            body.Value.Velocity.Y += dt * Gravity * body.Value.Mass;
          }
        }
      }

      if (body.Value.MotionType != Enums.MotionType.Static) {
        var x = body.Value.Velocity.X * dt;
        var y = body.Value.Velocity.Y * dt;

        body.Value.Velocity.X -= x;
        body.Value.Velocity.Y -= y;

        body.Value.Position.X += x;
        body.Value.Position.Y += y;
      }
    }
  }

  internal void HandleSprites(ReadOnlySpan<HammerObject> sprites) {
    var tilemaps = Bodies.Values.Where(x => x.ObjectType == Enums.ObjectType.Tilemap).ToArray();

    foreach (var sprite1 in sprites) {
      var collidesWithAnythingGround = false;
      foreach (var sprite2 in sprites) {
        if (sprite1 == sprite2) continue;

        var isColl = AABB.CheckCollisionMTV(sprite1, sprite2, out var mtv);
        if (isColl) {
          var dotProduct = Vector2.Dot(sprite1.Velocity, sprite2.Position);

          sprite1.Position += mtv;
          sprite1.Velocity.Y = 0;
        }
      }

      float spriteMinX = sprite1.Position.X;
      float spriteMaxX = spriteMinX + sprite1.AABB.Width;
      float spriteMinY = sprite1.Position.Y;
      float spriteMaxY = spriteMinY + sprite1.AABB.Height;

      HandleTilemaps(sprite1, tilemaps, ref collidesWithAnythingGround);

      if (collidesWithAnythingGround) {
        sprite1.Grounded = true;
      } else {
        sprite1.Grounded = false;
      }
    }
  }

  internal static void HandleTilemaps(HammerObject sprite, ReadOnlySpan<HammerObject> tilemaps, ref bool collidesWithAnythingGround) {
    foreach (var tilemap in tilemaps) {
      foreach (var aabb in tilemap.TilemapAABBs) {
        var isColl = AABB.CheckCollisionWithTilemapMTV(sprite.AABB, sprite.Position, aabb, tilemap.Position, out var mtv);
        if (isColl) {
          var dotProductX = Vector2.Dot(sprite.Velocity, sprite.Position);

          if (dotProductX > 0) {
            sprite.Position.X -= mtv.X;
          } else {
            sprite.Position.X += mtv.X;
          }

          if (sprite.Velocity.Y >= 0) {
            mtv.Y *= -1;
          }

          sprite.Velocity.Y = 0;
          sprite.Position.Y += mtv.Y;

          collidesWithAnythingGround = true;
        }
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