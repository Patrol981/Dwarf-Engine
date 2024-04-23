using Dwarf.Utils;

namespace Dwarf.Rendering.UI;
public partial class ImGuiController {
  // private readonly Dictionary<VulkanTexture, VkImageView> _autoViewsByTexture;
  private readonly List<string> _addedTextures = new List<string>();
  private readonly Dictionary<IntPtr, VulkanTexture> _userTextures = new();
  // private readonly Dictionary<VulkanTexture, IntPtr> _userTextures = new();
  private int _lastId = 100;

  public unsafe IntPtr GetOrCreateImGuiBinding(VulkanTexture texture) {
    if (texture == null) return IntPtr.Zero;
    if (!_addedTextures.Contains(texture.TextureName)) {
      texture.AddDescriptor(_systemSetLayout, _systemDescriptorPool);
      var descriptorSet = texture.VkTextureDescriptor.Handle;
      var ptr = MemoryUtils.ToIntPtr(descriptorSet);

      _userTextures.TryAdd(ptr, texture);
      _addedTextures.Add(texture.TextureName);
      return ptr;
    } else {
      var target = _userTextures.Where(x => x.Value.TextureName == texture.TextureName).FirstOrDefault();
      return target.Key;
    }
  }

  private IntPtr GetNextImGuiBinding() {
    int newId = _lastId++;
    return (IntPtr)newId;
  }
}
