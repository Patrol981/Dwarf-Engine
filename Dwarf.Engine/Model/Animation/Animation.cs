namespace Dwarf.Model.Animation;

public class Animation {
  public string Name = string.Empty;
  public List<AnimationSampler> Samplers = [];
  public List<AnimationChannel> Channels = [];
  public float Start;
  public float End;
}