using System.Numerics;

namespace Dwarf.Model.Animation;
public class AnimationSampler {
  public string Interpolation = string.Empty;
  public IList<float> Inputs = [];
  public List<Vector4> OutputsVec4 = [];
}
