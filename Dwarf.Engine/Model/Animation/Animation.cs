namespace Dwarf.Model.Animation;

public class Animation {
  public string Name;
  public List<AnimationSampler> Samplers;
  public List<AnimationChannel> Channels;
  public float Start;
  public float End;
}