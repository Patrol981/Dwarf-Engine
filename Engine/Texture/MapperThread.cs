using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Engine;

public class MapperThread {
  private readonly Device _device;
  private int _width;
  private int _height;
  private byte[] _data;

  public MapperThread(ref Device device, int w, int h, byte[] d) {
    _width = w;
    _height = h;
    _data = d;
    _device = device;
  }

  public void Process() {
    var size = _width * _height * 4;
    var stagingBuffer = new Vulkan.Buffer(
      _device,
      (ulong)(size),
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(_data), (ulong)size);
    stagingBuffer.Unmap();
  }
}