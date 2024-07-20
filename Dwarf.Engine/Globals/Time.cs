using static Dwarf.GLFW.GLFW;

namespace Dwarf.Globals;

public static class Time {
  private static double s_lastFrame = 0.0;
  private static double s_deltaTime = 0.0;

  private static double s_time = 0.0f;
  private static readonly double s_timeDelta = 0.01f;

  private static double s_currentTime = glfwGetTime();
  private static double s_accumulator = 0.0;

  public static void Tick_() {
    double currentFrame = glfwGetTime();
    s_deltaTime = currentFrame - s_lastFrame;
    s_lastFrame = currentFrame;
  }

  public static void Tick_2() {
    double newTime = glfwGetTime();
    double frameTime = newTime - s_currentTime;
    s_currentTime = newTime;

    s_accumulator += s_lastFrame;

    while (s_accumulator >= s_deltaTime) {
      s_accumulator -= s_deltaTime;
      s_time += s_deltaTime;
    }
  }

  public static void Tick() {
    double currentFrame = glfwGetTime();
    s_deltaTime = currentFrame - s_lastFrame;
    if (s_deltaTime > 0.25f) {
      s_deltaTime = 0.25f;
    }
    s_lastFrame = currentFrame;

    s_accumulator += s_deltaTime;
  }

  public static float DeltaTime => (float)s_deltaTime;
  public const float LOW_LIMIT = 0.0167f; // 60FPS
  public const float HIGH_LIMIT = 0.1f; // 10FPS
}