using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Model : Component, IDisposable {
  private readonly Device _device;

  private VkBuffer _vertexBuffer;
  private VkDeviceMemory _vertexBufferMemory;
  private ulong _vertexCount;

  public Model() { }

  public Model(Device device, Vertex[] vertices) {
    _device = device;
    CreateVertexBuffer(vertices);
  }

  public unsafe void Bind(VkCommandBuffer commandBuffer) {
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffer };
    ulong[] offsets = { 0 };
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }
  }

  public void Draw(VkCommandBuffer commandBuffer) {
    vkCmdDraw(commandBuffer, (int)_vertexCount, 1, 0, 0);
  }

  private unsafe void CreateVertexBuffer(Vertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;
    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount;
    _device.CreateBuffer(
      bufferSize,
      VkBufferUsageFlags.VertexBuffer,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
      out _vertexBuffer,
      out _vertexBufferMemory
    );

    // IntPtr data = nint.Zero;
    void* data;
    vkMapMemory(_device.LogicalDevice, _vertexBufferMemory, 0, bufferSize, VkMemoryMapFlags.None, &data).CheckResult();
    Utils.MemCopy((IntPtr)data, ToIntPtr(vertices), (int)bufferSize);
    vkUnmapMemory(_device.LogicalDevice, _vertexBufferMemory);
  }

  public unsafe void Dispose() {
    vkDestroyBuffer(_device.LogicalDevice, _vertexBuffer);
    vkFreeMemory(_device.LogicalDevice, _vertexBufferMemory);
  }

  public static IntPtr ToIntPtr<T>(T[] arr) where T : struct {
    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;
    try {
      ptr = Marshal.AllocHGlobal(size * arr.Length);
      for (int i = 0; i < arr.Length; i++) {
        Marshal.StructureToPtr(arr[i], IntPtr.Add(ptr, i * size), true);
      }
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }

  public unsafe static VkVertexInputBindingDescription* GetBindingDescsFunc() {
    var bindingDescriptions = new VkVertexInputBindingDescription[1];
    bindingDescriptions[0].binding = 0;
    bindingDescriptions[0].stride = ((uint)Unsafe.SizeOf<Vertex>());
    bindingDescriptions[0].inputRate = VkVertexInputRate.Vertex;
    fixed (VkVertexInputBindingDescription* ptr = bindingDescriptions) {
      return ptr;
    }
  }

  public static uint GetBindingsLength() => 1;

  public unsafe static VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    var attributeDescriptions = new VkVertexInputAttributeDescription[2];
    attributeDescriptions[0].binding = 0;
    attributeDescriptions[0].location = 0;
    attributeDescriptions[0].format = VkFormat.R32G32Sfloat;
    attributeDescriptions[0].offset = (uint)Marshal.OffsetOf<Vertex>("Position");

    attributeDescriptions[1].binding = 0;
    attributeDescriptions[1].location = 1;
    attributeDescriptions[1].format = VkFormat.R32G32B32Sfloat;
    attributeDescriptions[1].offset = (uint)Marshal.OffsetOf<Vertex>("Color");

    fixed (VkVertexInputAttributeDescription* ptr = attributeDescriptions) {
      return ptr;
    }
  }
  public static uint GetAttribsLength() => 2;
}