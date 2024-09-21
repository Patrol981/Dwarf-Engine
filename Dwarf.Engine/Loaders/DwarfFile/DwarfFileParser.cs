using System.Text.Json;
using System.Text.Json.Serialization;
using Dwarf.EntityComponentSystem;

namespace Dwarf.Loaders;

public static class DwarfFileParser {
  public readonly static JsonSerializerOptions ParserOptions = new() {
    WriteIndented = false,
    ReferenceHandler = ReferenceHandler.Preserve
  };

  public static DwarfFile? Parse(in Entity entity) {
    var meshRenderer = entity.GetComponent<MeshRenderer>();

    if (meshRenderer == null) return null;

    return DwarfFile.ToDwarfFile(meshRenderer);
  }

  public static void SaveToFile(string path, DwarfFile file) {
    // run it in a thread pool so it's not blocking the main thread
    Task.Run(() => {
      var outputString = JsonSerializer.Serialize<DwarfFile>(file, ParserOptions);
      File.WriteAllText(path, outputString);

      return Task.CompletedTask;
    });
  }
}