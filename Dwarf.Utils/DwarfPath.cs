using System.Reflection;

namespace Dwarf.Utils;

public static class DwarfPath {
  public static string AssemblyDirectory {
    get {
      string? codeBase = (Assembly.GetEntryAssembly()?.Location) ?? throw new Exception("Could not found proper assembly.");
      if (string.IsNullOrEmpty(codeBase)) {
        codeBase = AppContext.BaseDirectory;
      }
      UriBuilder uri = new UriBuilder(codeBase);
      string path = Uri.UnescapeDataString(uri.Path);
      return Path.GetDirectoryName(path)!;
    }
  }
}