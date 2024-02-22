using static Dwarf.GLFW.GLFW;

using Dwarf.Engine.Globals;

namespace Dwarf.Engine.Global;
public static class Frames {
  private static int s_frameCount = 0;
  private static double s_prevTime = glfwGetTime();
  private static DateTime s_startTime;
  private static DateTime s_endTime;
  private static double s_frameRate = 0.0f;

  public static float GetFramesDelta() {
    var lastUpdate = Time.DeltaTime;
    return lastUpdate;
  }

  public static double GetFrames() {
    // return (s_endTime - s_startTime).TotalMilliseconds;
    return Time.DeltaTime;
    // return MathF.Round(, 5, MidpointRounding.ToZero);
  }

  public static void TickStart() {
    s_startTime = DateTime.UtcNow;
  }

  public static void TickEnd() {
    s_endTime = DateTime.UtcNow;
  }

  /*
  public static double GetFrames() {
    double currentTime = glfwGetTime();
    s_frameCount++;
    if (currentTime - s_prevTime >= 1.0) {
      s_frameRate = s_frameCount;
      s_frameCount = 0;
      s_prevTime = currentTime;
    }
    return s_frameRate;
  }
  */
}
