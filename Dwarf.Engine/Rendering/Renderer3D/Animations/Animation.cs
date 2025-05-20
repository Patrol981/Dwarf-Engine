namespace Dwarf.Rendering.Renderer3D.Animations;

public class Animation : ICloneable {
  public string Name = string.Empty;
  public List<AnimationSampler> Samplers = [];
  public List<AnimationChannel> Channels = [];
  public float Start;
  public float End;

  public object Clone() {
    return new Animation {
      Name = Name,
      Samplers = Samplers.Select(s => { return (AnimationSampler)s.Clone(); }).ToList(),
      Channels = Channels.Select(c => { return (AnimationChannel)c.Clone(); }).ToList(),
      Start = Start,
      End = End
    };
  }
}