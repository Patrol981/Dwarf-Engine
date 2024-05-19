namespace Dwarf.Model.Animation;
public class AnimationObject {
  public string Name = string.Empty;
  public IList<AnimationSampler> Samplers = [];
  public IList<AnimationChannel> Channels = [];
  public float Start = float.MaxValue;
  public float End = float.MinValue;
  public float Current = 0.0f;
}
