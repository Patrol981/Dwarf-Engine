
using System.Runtime.CompilerServices;
using System;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Physics;
public class ColliderMesh : Component, IDebugRender3DObject {
  private readonly Device _device = null!;

  private Vulkan.Buffer _vertexBuffer = null!;
  private Vulkan.Buffer _indexBuffer = null!;
  private ulong _vertexCount = 0;
  private ulong _indexCount = 0;

  private Mesh _mesh;
  private bool _finishedInitialization = false;
  private bool _hasIndexBuffer = false;

  private bool _enabled = true;

  public ColliderMesh() { }

  public ColliderMesh(Device device, Mesh mesh) {
    _device = device;
    _mesh = mesh;

    if (_mesh.Indices.Length > 0) _hasIndexBuffer = true;

    Init();
  }

  public async void Init() {
    Task[] tasks = new Task[2] {
      CreateVertexBuffer(_mesh.Vertices),
      CreateIndexBuffer(_mesh.Indices)
    };
    await Task.WhenAll(tasks);
    _finishedInitialization = true;
  }

  public void Bind(VkCommandBuffer commandBuffer) {
    throw new NotImplementedException();
  }

  public unsafe Task Bind(VkCommandBuffer commandBuffer, uint index = 0) {
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffer.GetBuffer() };
    ulong[] offsets = { 0 };
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }

    if (_hasIndexBuffer) {
      vkCmdBindIndexBuffer(commandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint32);
    }
    return Task.CompletedTask;
  }

  public void BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout) {
    throw new Exception("Colldier mesh should not have textures");
  }

  public void Dispose() {
    _vertexBuffer.Dispose();
    if (_hasIndexBuffer) {
      _indexBuffer.Dispose();
    }
  }

  public Task Draw(VkCommandBuffer commandBuffer, uint index = 0) {
    if (_hasIndexBuffer) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount, 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount, 1, 0, 0);
    }
    return Task.CompletedTask;
  }

  private unsafe Task CreateVertexBuffer(Vertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;
    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount;
    ulong vertexSize = ((ulong)Unsafe.SizeOf<Vertex>());

    var stagingBuffer = new Dwarf.Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount,
      VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  private unsafe Task CreateIndexBuffer(uint[] indices) {
    _indexCount = (ulong)indices.Length;
    if (!_hasIndexBuffer) return Task.CompletedTask;
    ulong bufferSize = sizeof(uint) * _indexCount;
    ulong indexSize = sizeof(uint);

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    // vkDeviceWaitIdle(_device.LogicalDevice);
    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(indices), bufferSize);
    //stagingBuffer.Unmap();

    _indexBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  public bool UsesTexture => false;
  public bool UsesLight {
    get { return false; }
    set { }
  }

  public int MeshsesCount => 1;

  public bool FinishedInitialization => _finishedInitialization;

  public Mesh Mesh => _mesh;

  public bool Enabled => _enabled;
  public void Enable() {
    _enabled = true;
  }
  public void Disable() {
    _enabled = false;
  }
}
