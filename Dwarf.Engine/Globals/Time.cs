using System.Diagnostics;
using static SDL3.SDL3;

namespace Dwarf.Globals;

public static class Time {
  private static ulong s_lastFrame = 0;
  private static double s_deltaTime = 0.0;
  private static double s_fixedTime = 0.0;

  private static double s_time = 0.0;

  private static ulong s_currentTime = 0;
  private static double s_accumulator = 0.0;

  private static ulong s_frequency = 0;

  private static Stopwatch s_stopwatch = new();
  private static long s_lastStopwatchTick = 0;
  private static float s_stopwatchDelta;

  private static DateTime s_startTime;

  public static void Init() {
    s_frequency = SDL_GetPerformanceFrequency();
    s_currentTime = SDL_GetPerformanceCounter();
    s_stopwatch.Start();
    s_lastStopwatchTick = s_stopwatch.ElapsedTicks;
    s_startTime = DateTime.Now;
  }

  public static void Tick_() {
    double currentFrame = SDL_GetPerformanceCounter();
    s_deltaTime = (currentFrame - s_lastFrame) / s_frequency;
    // s_lastFrame = currentFrame;
  }


  public static void Tick() {
    ulong currentFrame = SDL_GetPerformanceCounter();
    ulong frameTicks = currentFrame - s_lastFrame;

    s_deltaTime = (double)frameTicks / s_frequency;
    s_fixedTime = s_deltaTime;

    if (s_deltaTime > 0.25) {
      s_deltaTime = 0.25;
    }

    s_lastFrame = currentFrame;

    // Alternative

    long currentFrameTick = s_stopwatch.ElapsedTicks;
    long frameTick = currentFrameTick - s_lastStopwatchTick;
    s_stopwatchDelta = (float)frameTick / Stopwatch.Frequency;
    s_lastStopwatchTick = currentFrameTick;
  }

  public static void Tick_2() {
    double newTime = SDL_GetTicks();
    double frameTime = newTime - s_currentTime;
    // s_currentTime = newTime;

    s_accumulator += s_lastFrame;

    while (s_accumulator >= s_deltaTime) {
      s_accumulator -= s_deltaTime;
      s_time += s_deltaTime;
    }
  }

  public static float DeltaTime => s_stopwatchDelta;
  public static float FixedTime => (float)s_fixedTime;
  public static long StopwatchTick => s_lastStopwatchTick;
  public static float StopwatchDelta => s_stopwatchDelta;
  public static double Frequency => s_frequency;
  public static double CurrentTime => s_currentTime;
  public static double Accumulator => s_accumulator;
  public static double LastFrame => s_lastFrame;
  public static double STime => s_time;
  public const double LOW_LIMIT = 0.0167; // 60FPS
  public const double HIGH_LIMIT = 0.1; // 10FPS
  public static DateTime StartTime => s_startTime;
}