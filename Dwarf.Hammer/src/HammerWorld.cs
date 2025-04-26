using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerWorld {
  internal Dictionary<BodyId, HammerObject> Bodies = [];
  internal float Gravity = 9.80665f;
  const float THRESHOLD = 0.5f;

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
        HandleGrounded(body.Value, dt);
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

  internal void HandleGrounded(in HammerObject body, float dt) {
    if (body.Grounded) {
      if (body.Force.Y < 0) {
        body.Velocity.Y -= dt * Gravity * body.Mass;
        body.Grounded = false;
      }

      return;
    }

    if (body.Force.Y < 0) {
      body.Velocity.Y -= dt * Gravity * body.Mass;
      body.Force.Y += dt * Gravity;
    } else {
      body.Velocity.Y += dt * Gravity * body.Mass;
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
      var aabbss = SortOutTilemap(sprite, tilemap);
      foreach (var aabb in aabbss) {
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

  private static ReadOnlySpan<AABB> SortOutTilemap(HammerObject sprite, HammerObject tilemap) {
    var aabbToCheck = new List<AABB>();

    foreach (var aabb in tilemap.TilemapAABBs) {
      var distance = Vector2.Distance(sprite.Position, aabb.Max);
      if (distance < THRESHOLD) {
        aabbToCheck.Add(aabb);
      }
    }

    return aabbToCheck.ToArray();
  }

  internal BodyId AddBody(Vector2 position) {
    var bodyId = new BodyId();
    var hammerObject = new HammerObject {
      Position = position
    };
    Bodies.Add(bodyId, hammerObject);
    return bodyId;
  }

  internal void RemoveBody(in BodyId bodyId) {
    Bodies.Remove(bodyId);
  }
}