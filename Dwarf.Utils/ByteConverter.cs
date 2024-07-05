using System.Text;

namespace Dwarf.Utils;
public static class ByteConverter {
  public static unsafe string BytePointerToStringUTF8(byte* bytes) {
    if (bytes == null) throw new ArgumentNullException(nameof(bytes));

    int length = 0;
    while (bytes[length] != 0) length += 1;

    return Encoding.UTF8.GetString(bytes, length);
  }
}
