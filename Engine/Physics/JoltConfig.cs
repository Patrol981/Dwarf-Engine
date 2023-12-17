namespace Dwarf.Engine.Physics;
public static class JoltConfig {
  public static uint MaxBodies { get; private set; } = 1024;
  public static uint NumBodyMutexes { get; private set; } = 0;
  public static uint MaxBodyPairs { get; private set; } = 1024;
  public static uint MaxContactConstraints { get; private set; } = 1024;

  public static float WorldScale = 1.0f;

  internal static class Layers {
    public const byte NonMoving = 0;
    public const byte Moving = 1;
    public const int NumLayers = 2;
  }

  internal static class BroadPhaseLayers {
    public const byte NonMoving = 0;
    public const byte Moving = 1;
    public const int NumLayers = 2;
  }
}
