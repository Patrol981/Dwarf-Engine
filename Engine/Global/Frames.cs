using static Dwarf.Extensions.GLFW.GLFW;

using Dwarf.Engine.Globals;

namespace Dwarf.Engine.Global;
public static class Frames {
  private static int s_frameCount = 0;
  private static double s_prevTime = glfwGetTime();
  private static double s_frameRate = 0.0f;

  public static float GetFramesDelta() {
    var lastUpdate = Time.DeltaTime;
    return lastUpdate;
  }

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
}
