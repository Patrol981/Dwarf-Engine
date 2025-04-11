namespace Dwarf.Rendering.Renderer3D.Animations;

public class AnimationChannel : ICloneable {
  public enum PathType {
    Translation,
    Rotation,
    Scale
  }

  public PathType Path;
  public Node Node = null!;
  public int SamplerIndex;

  public object Clone() {
    return new AnimationChannel {
      Path = Path,
      // Node = Node,
      SamplerIndex = SamplerIndex
    };
  }
}