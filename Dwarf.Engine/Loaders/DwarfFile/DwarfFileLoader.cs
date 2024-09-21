using System.Text.Json;

namespace Dwarf.Loaders;

public static class DwarfFileLoader {
  public static DwarfFile Load(string path) {
    try {
      var file = LoadFromFile(path);

      return JsonSerializer.Deserialize<DwarfFile>(file, DwarfFileParser.ParserOptions)
      ?? throw new Exception("File is null");
    } catch {
      throw;
    }
  }

  private static string LoadFromFile(string path) {
    return File.ReadAllText(path);
  }
}