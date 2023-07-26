using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;

namespace DwarfEngine.Engine.Math;
public class Collision2D {
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
