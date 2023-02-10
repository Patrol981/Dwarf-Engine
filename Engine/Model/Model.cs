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
  private readonly Device _device = null!;

  private Dwarf.Vulkan.Buffer[] _vertexBuffers = new Vulkan.Buffer[0];
  private ulong[] _vertexCount = new ulong[0];

  private bool[] _hasIndexBuffer = new bool[0];
  private Dwarf.Vulkan.Buffer[] _indexBuffers = new Vulkan.Buffer[0];
  private ulong[] _indexCount = new ulong[0];

  private int _meshesCount = 0;

  public int MeshsesCount => _meshesCount;

  public Model() { }

  public Model(Device device, Mesh[] meshes) {
    _device = device;
    _indexCount = new ulong[meshes.Length];
    _indexBuffers = new Vulkan.Buffer[meshes.Length];
    _indexCount = new ulong[meshes.Length];
    _vertexBuffers = new Vulkan.Buffer[meshes.Length];
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
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffers[index].GetBuffer() };
    ulong[] offsets = { 0 };
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }

    if (_hasIndexBuffer[index]) {
      vkCmdBindIndexBuffer(commandBuffer, _indexBuffers[index].GetBuffer(), 0, VkIndexType.Uint32);
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
    ulong vertexSize = ((ulong)Unsafe.SizeOf<Vertex>());

    var stagingBuffer = new Dwarf.Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount[index],
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(Utils.ToIntPtr(vertices), bufferSize);

    _vertexBuffers[index] = new Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount[index],
      VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffers[index].GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(uint[] indices, uint index) {
    _indexCount[index] = (ulong)indices.Length;
    // _hasIndexBuffer = _indexCount[index] > 0;
    if (!_hasIndexBuffer[index]) return;
    ulong bufferSize = (ulong)sizeof(uint) * _indexCount[index];
    ulong indexSize = (ulong)sizeof(uint);

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount[index],
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(Utils.ToIntPtr(indices), bufferSize);
    //stagingBuffer.Unmap();

    // var stagingBuffer = new VkBuffer();
    // var stagingBufferMemory = new VkDeviceMemory();

    /*
    _device.CreateBuffer(
      bufferSize,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
      out stagingBuffer,
      out stagingBufferMemory
    );
    */

    // void* data;
    // vkMapMemory(_device.LogicalDevice, stagingBufferMemory, 0, bufferSize, VkMemoryMapFlags.None, &data).CheckResult();
    // Utils.MemCopy((IntPtr)data, Utils.ToIntPtr(indices), (int)bufferSize);
    // vkUnmapMemory(_device.LogicalDevice, stagingBufferMemory);

    _indexBuffers[index] = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount[index],
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );


    /*
    _device.CreateBuffer(
      bufferSize,
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal,
      out _indexBuffers[index],
      out _indexBufferMemories[index]
    );
    */

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffers[index].GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    // _indexBuffers[index].Dispose();
    //vkDestroyBuffer(_device.LogicalDevice, stagingBuffer);
    //vkFreeMemory(_device.LogicalDevice, stagingBufferMemory);
  }

  public unsafe void Dispose() {
    for (int i = 0; i < _vertexBuffers.Length; i++) {
      //vkDestroyBuffer(_device.LogicalDevice, _vertexBuffers[i]);
      //vkFreeMemory(_device.LogicalDevice, _vertexBufferMemories[i]);

      _vertexBuffers[i].Dispose();

      if (_hasIndexBuffer[i]) {
        // vkDestroyBuffer(_device.LogicalDevice, _indexBuffers[i]);
        // vkFreeMemory(_device.LogicalDevice, _indexBufferMemories[i]);
        _indexBuffers[i].Dispose();
      }
    }
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