using static SDL3.SDL3;

namespace Dwarf.Globals;

public static class Time {
  private static double s_lastFrame = 0.0;
  private static double s_deltaTime = 0.0;
  private static double s_fixedTime = 0.0;

  private static double s_time = 0.0f;
  // private static readonly double s_timeDelta = 0.01f;

  private static double s_currentTime = 0.0f;
  private static double s_accumulator = 0.0f;

  private static double s_frequency = 0.0f;

  public static void Init() {
    s_frequency = SDL_GetPerformanceFrequency();
    s_currentTime = SDL_GetPerformanceCounter();
  }

  public static void Tick_() {
    double currentFrame = SDL_GetPerformanceCounter();
    s_deltaTime = (currentFrame - s_lastFrame) / s_frequency;
    s_lastFrame = currentFrame;
  }


  public static void Tick() {
    double currentFrame = SDL_GetPerformanceCounter();
    s_deltaTime = (currentFrame - s_lastFrame) / s_frequency;
    s_fixedTime = s_deltaTime;
    if (s_deltaTime > 0.25f) {
      s_deltaTime = 0.25f;
    }
    s_lastFrame = currentFrame;

    s_accumulator += s_deltaTime;
  }

  public static void Tick_2() {
    double newTime = SDL_GetTicks();
    double frameTime = newTime - s_currentTime;
    s_currentTime = newTime;

    s_accumulator += s_lastFrame;

    while (s_accumulator >= s_deltaTime) {
      s_accumulator -= s_deltaTime;
      s_time += s_deltaTime;
    }
  }

  public static float DeltaTime => (float)s_deltaTime;
  public static float FixedTime => (float)s_fixedTime;
  public const float LOW_LIMIT = 0.0167f; // 60FPS
  public const float HIGH_LIMIT = 0.1f; // 10FPS
}