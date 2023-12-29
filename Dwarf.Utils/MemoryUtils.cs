namespace Dwarf.Utils;

public static class MemoryUtils {
  public static bool IsNull(byte[] bytes) {
    bool isNull = true;
    for (int i = 0; i < bytes.Length; i++) {
      if (bytes[i] != 0) {
        isNull = false;
        break;
      }
    }
    return isNull;
  }
}