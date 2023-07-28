using System.Diagnostics;

using Dwarf.Extensions.Logging;

using static Dwarf.Extensions.GLFW.GLFW;

namespace Dwarf.Engine.Globals;

public static class Time {
  private static double s_LastFrame = 0.0;
  private static double s_DeltaTime = 0.0;
  public static float DeltaTime => (float)s_DeltaTime;
  public static void Tick() {
    double currentFrame = glfwGetTime();
    s_DeltaTime = currentFrame - s_LastFrame;
    s_LastFrame = currentFrame;

    // var scrollDelta = MouseState.GetInstance().ScrollDelta;
    // scrollDelta.Y = MathF.Floor((float)scrollDelta.Y / 2.0f);
    // Logger.Info($"[SCROLL DELTA] {scrollDelta}");
    // MouseState.GetInstance().ScrollDelta = 0.0f;
  }
}