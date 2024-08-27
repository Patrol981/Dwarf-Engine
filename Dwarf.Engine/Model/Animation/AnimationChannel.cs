namespace Dwarf.Model.Animation;

public class AnimationChannel {
  public enum PathType {
    Translation,
    Rotation,
    Scale
  }

  public PathType Path;
  public Node Node = null!;
  public int SamplerIndex;
}