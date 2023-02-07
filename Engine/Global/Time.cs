using System.Diagnostics;

namespace Dwarf.Engine.Globals;

public static class Time {
  private static double s_currentFrame;
  public static float DeltaTime => (float)s_currentFrame;
  private static Stopwatch s_Stopwatch = new();
  public static void StartTick() {
    s_Stopwatch.Start();
  }
  public static void EndTick() {
    s_Stopwatch.Stop();
    s_currentFrame = s_Stopwatch.Elapsed.TotalSeconds;
    s_Stopwatch.Reset();
  }
}