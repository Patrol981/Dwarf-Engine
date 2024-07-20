using System.Drawing;

namespace Dwarf.Rendering.UI.FontStash;
public class FontStashManager : FontStashSharp.Interfaces.ITexture2DManager {
  public FontStashManager() { }

  public object CreateTexture(int width, int height) {
    var device = Application.Instance.Device;
    return new VulkanTexture(device, width, height);
  }
  public Point GetTextureSize(object texture) {
    var t = (VulkanTexture)texture;
    return new(t.Size);
  }

  public void SetTextureData(object texture, Rectangle bounds, byte[] data) {
    var t = (VulkanTexture)texture;
    t.SetTextureData(data);
  }
}
