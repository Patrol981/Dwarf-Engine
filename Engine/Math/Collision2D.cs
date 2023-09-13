﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;
using Dwarf.Extensions.Logging;

namespace DwarfEngine.Engine.Math;
public class Collision2D {
  public static bool MouseClickedCollision(I2DCollision coll, Camera camera, Vector2 screenSize) {
    var mouseRay = Ray.MouseToWorld2D(camera, screenSize);
    var component = (Component)coll;
    var position = component!.Owner!.GetComponent<RectTransform>().Position;

    /*
    Logger.Info($"[RAY] {mouseRay}");
    Logger.Info($"[POS] {position}");
    Logger.Info($"[SIZE] {coll.Size}");
    Logger.Info($"[MIN] {coll.Bounds.Min}");
    Logger.Info($"[MAX] {coll.Bounds.Max}");
    */

    var bounds = coll.Bounds;

    var collides =
      mouseRay.X >= bounds.Min.X && mouseRay.X <= bounds.Max.X &&
      mouseRay.Y >= bounds.Min.Y && mouseRay.Y <= bounds.Max.Y;

    return collides;
  }

  public static bool CheckCollisionAABB(I2DCollision a, I2DCollision b) {
    var compA = a as Component;
    var compB = b as Component;

    if (a.IsUI && !b.IsUI) throw new Exception("Cannot compare UI and non UI element");

    if (a.IsUI) {
      var aTransform = compA!.Owner!.GetComponent<RectTransform>();
      var bTransform = compB!.Owner!.GetComponent<RectTransform>();

      if (
      aTransform.Position.X < bTransform.Position.X + b.Size.X &&
      aTransform.Position.X + a.Size.X > bTransform.Position.X &&
      aTransform.Position.Y < bTransform.Position.Y + b.Size.Y &&
      a.Size.Y + aTransform.Position.Y > bTransform.Position.Y
    ) {
        return true;
      }
      return false;

    } else {
      var aTransform = compA!.Owner!.GetComponent<Transform>();
      var bTransform = compB!.Owner!.GetComponent<Transform>();

      if (
      aTransform.Position.X < bTransform.Position.X + b.Size.X &&
      aTransform.Position.X + a.Size.X > bTransform.Position.X &&
      aTransform.Position.Y < bTransform.Position.Y + b.Size.Y &&
      a.Size.Y + aTransform.Position.Y > bTransform.Position.Y
    ) {
        return true;
      }
      return false;
    }
  }

  public static bool CheckCollisionAABB(Sprite a, Sprite b) {
    var aTransform = a.Owner!.GetComponent<Transform>();
    var bTransform = b.Owner!.GetComponent<Transform>();

    if (
      aTransform.Position.X < bTransform.Position.X + b.Size.X &&
      aTransform.Position.X + a.Size.X > bTransform.Position.X &&
      aTransform.Position.Y < bTransform.Position.Y + b.Size.Y &&
      a.Size.Y + aTransform.Position.Y > bTransform.Position.Y
    ) {
      return true;
    }
    return false;
  }

  public static ReadOnlySpan<Sprite> CollidesWithAABB(Entity[] colls2D, Entity target) {
    List<Sprite> colliders = new List<Sprite>();
    ReadOnlySpan<Entity> withoutTarget = colls2D
                                          .Where(x => x.EntityID != target.EntityID)
                                          .ToArray();
    var targetSprite = target.GetComponent<Sprite>();
    for (int i = 0; i < withoutTarget.Length; i++) {
      var iSprite = withoutTarget[i].GetComponent<Sprite>();
      if (CheckCollisionAABB(targetSprite, iSprite)) {
        colliders.Add(iSprite);
      }
    }

    return colliders.ToArray();
  }
}
