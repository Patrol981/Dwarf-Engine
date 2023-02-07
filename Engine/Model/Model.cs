using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using OpenTK.Mathematics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Model : Component, IDisposable {
  private readonly Device _device;

  private VkBuffer[] _vertexBuffers;
  private VkDeviceMemory[] _vertexBufferMemories;
  private ulong[] _vertexCount;

  private bool[] _hasIndexBuffer;
  private VkBuffer[] _indexBuffers;
  private VkDeviceMemory[] _indexBufferMemories;
  private ulong[] _indexCount;

  private int _meshesCount = 0;

  public int MeshsesCount => _meshesCount;

  public Model() { }

  public Model(Device device, Mesh[] meshes) {
    _device = device;
    _indexCount = new ulong[meshes.Length];
    _indexBuffers = new VkBuffer[meshes.Length];
    _indexBufferMemories = new VkDeviceMemory[meshes.Length];
    _indexCount = new ulong[meshes.Length];
    _vertexBuffers = new VkBuffer[meshes.Length];
    _vertexBufferMemories = new VkDeviceMemory[meshes.Length];
    _vertexCount = new ulong[meshes.Length];
    _hasIndexBuffer = new bool[meshes.Length];
    _meshesCount = meshes.Length;
    for (uint i = 0; i < meshes.Length; i++) {
      if (meshes[i].Indices.Length > 0) _hasIndexBuffer[i] = true;
      CreateVertexBuffer(meshes[i].Vertices, i);
      CreateIndexBuffer(meshes[i].Indices, i);
    }
  }

  public unsafe void Bind(VkCommandBuffer commandBuffer, uint index) {
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffers[index] };
    // VkBuffer[] buffers = _vertexBuffers.ToArray();
    ulong[] offsets = { 0 };
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }

    if (_hasIndexBuffer[index]) {
      vkCmdBindIndexBuffer(commandBuffer, _indexBuffers[index], 0, VkIndexType.Uint32);
    }
  }

  public void Draw(VkCommandBuffer commandBuffer, uint index) {
    if (_hasIndexBuffer[index]) {
      vkCmdDrawIndexed(commandBuffer, (int)_indexCount[index], 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (int)_vertexCount[index], 1, 0, 0);
    }
  }

  private unsafe void CreateVertexBuffer(Vertex[] vertices, uint index) {
    _vertexCount[index] = (ulong)vertices.Length;
    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount[index];

    var stagingBuffer = new VkBuffer();
    var stagingBufferMemory = new VkDeviceMemory();

    _device.CreateBuffer(
      bufferSize,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
      out stagingBuffer,
      out stagingBufferMemory
    );

    // IntPtr data = nint.Zero;
    void* data;
    vkMapMemory(_device.LogicalDevice, stagingBufferMemory, 0, bufferSize, VkMemoryMapFlags.None, &data).CheckResult();
    Utils.MemCopy((IntPtr)data, ToIntPtr(vertices), (int)bufferSize);
    vkUnmapMemory(_device.LogicalDevice, stagingBufferMemory);

    _device.CreateBuffer(
      bufferSize,
      VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal,
      out _vertexBuffers[index],
      out _vertexBufferMemories[index]
    );

    _device.CopyBuffer(stagingBuffer, _vertexBuffers[index], bufferSize);
    vkDestroyBuffer(_device.LogicalDevice, stagingBuffer);
    vkFreeMemory(_device.LogicalDevice, stagingBufferMemory);
  }

  private unsafe void CreateIndexBuffer(uint[] indices, uint index) {
    _indexCount[index] = (ulong)indices.Length;
    // _hasIndexBuffer = _indexCount[index] > 0;
    if (!_hasIndexBuffer[index]) return;
    ulong bufferSize = (ulong)sizeof(uint) * _indexCount[index];

    var stagingBuffer = new VkBuffer();
    var stagingBufferMemory = new VkDeviceMemory();

    _device.CreateBuffer(
      bufferSize,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
      out stagingBuffer,
      out stagingBufferMemory
    );

    void* data;
    vkMapMemory(_device.LogicalDevice, stagingBufferMemory, 0, bufferSize, VkMemoryMapFlags.None, &data).CheckResult();
    Utils.MemCopy((IntPtr)data, ToIntPtr(indices), (int)bufferSize);
    vkUnmapMemory(_device.LogicalDevice, stagingBufferMemory);

    _device.CreateBuffer(
      bufferSize,
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal,
      out _indexBuffers[index],
      out _indexBufferMemories[index]
    );

    _device.CopyBuffer(stagingBuffer, _indexBuffers[index], bufferSize);
    vkDestroyBuffer(_device.LogicalDevice, stagingBuffer);
    vkFreeMemory(_device.LogicalDevice, stagingBufferMemory);
  }

  public unsafe void Dispose() {
    for (int i = 0; i < _vertexBuffers.Length; i++) {
      vkDestroyBuffer(_device.LogicalDevice, _vertexBuffers[i]);
      vkFreeMemory(_device.LogicalDevice, _vertexBufferMemories[i]);

      if (_hasIndexBuffer[i]) {
        vkDestroyBuffer(_device.LogicalDevice, _indexBuffers[i]);
        vkFreeMemory(_device.LogicalDevice, _indexBufferMemories[i]);
      }
    }
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
    var attributeDescriptions = new VkVertexInputAttributeDescription[GetAttribsLength()];
    attributeDescriptions[0].binding = 0;
    attributeDescriptions[0].location = 0;
    attributeDescriptions[0].format = VkFormat.R32G32B32Sfloat;
    attributeDescriptions[0].offset = (uint)Marshal.OffsetOf<Vertex>("Position");

    attributeDescriptions[1].binding = 0;
    attributeDescriptions[1].location = 1;
    attributeDescriptions[1].format = VkFormat.R32G32B32Sfloat;
    attributeDescriptions[1].offset = (uint)Marshal.OffsetOf<Vertex>("Color");

    attributeDescriptions[2].binding = 0;
    attributeDescriptions[2].location = 2;
    attributeDescriptions[2].format = VkFormat.R32G32B32Sfloat;
    attributeDescriptions[2].offset = (uint)Marshal.OffsetOf<Vertex>("Normal");

    attributeDescriptions[3].binding = 0;
    attributeDescriptions[3].location = 3;
    attributeDescriptions[3].format = VkFormat.R32G32Sfloat;
    attributeDescriptions[3].offset = (uint)Marshal.OffsetOf<Vertex>("Uv");

    fixed (VkVertexInputAttributeDescription* ptr = attributeDescriptions) {
      return ptr;
    }
  }
  public static uint GetAttribsLength() => 4;
}