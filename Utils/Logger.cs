namespace Dwarf.Extensions.Logging;

public static class Logger {
  public static void Info(string message) {
    WriteColored(ConsoleColor.Green, "[INFO]");
    Console.WriteLine(" " + message);
  }

  public static void Warn(string message) {
    WriteColored(ConsoleColor.Yellow, "[WARN]");
    Console.WriteLine(" " + message);
  }

  public static void Error(string message) {
    WriteColored(ConsoleColor.Red, "[ERROR]");
    Console.WriteLine(" " + message);
  }

  private static void WriteColored(ConsoleColor color, string message) {
    var currentColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(message);
    Console.ForegroundColor = currentColor;
  }
}