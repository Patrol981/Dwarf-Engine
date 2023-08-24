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

  public RectTransform() : base() { }

  public RectTransform(Vector3 position) : base(position) { }
}
