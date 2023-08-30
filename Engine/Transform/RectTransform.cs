using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.Rendering.UI;

namespace Dwarf.Engine;
public class RectTransform : Transform {
  public Anchor Anchor { get; set; }
  public Vector2 OffsetFromVector { get; set; }
  public float OriginScale { get; set; } = 1.0f;
  internal uint LastScreenX { get; set; } = 0;
  internal uint LastScreenY { get; set; } = 0;
  internal float LastGlobalScale { get; set; } = 0.0f;

  public RectTransform() : base() { }

  public RectTransform(Vector3 position) : base(position) { }
}
