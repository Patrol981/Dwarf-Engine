using System.Reflection;

namespace Dwarf.Utils;

public static class DwarfPath {
  public static string AssemblyDirectory {
    get {
      try {
        return AppContext.BaseDirectory;
      } catch (Exception ex) {
        throw new Exception($"[ERROR] Failed to get assembly directory: {ex.Message}");
      }
    }
  }
  public static string AssemblyDirectoryReflected {
    get {
      string? codeBase = Assembly.GetEntryAssembly()?.Location
                ?? throw new Exception("Could not find the proper assembly.");

      // Ensure the path is valid
      if (string.IsNullOrEmpty(codeBase)) {
        return AppContext.BaseDirectory;
      }

      // Convert to a valid file path
      return Path.GetDirectoryName(codeBase)
          ?? throw new Exception("Could not determine the assembly directory.");
    }
  }

  public static string UserDocuments {
    get {
      string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

      return path;
    }
  }

  private static string AssemblyDirectoryOld {
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