using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Rendering;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class MeshRenderer : Component, IRender3DElement, ICollision {
  private readonly Device _device = null!;

  private Vulkan.Buffer[] _vertexBuffers = [];
  private ulong[] _vertexCount = [];
  private bool[] _hasIndexBuffer = [];
  private Vulkan.Buffer[] _indexBuffers = [];
  private ulong[] _indexCount = [];
  private int _meshesCount = 0;
  private Guid[] _textureIdRefs = [];

  private Mesh[] _meshes = [];
  private AABB[] _aabbes = [];
  private AABB _mergedAABB = new();

  private bool _finishedInitialization = false;

  public MeshRenderer() { }

  public MeshRenderer(Device device) {
    _device = device;
  }

  public MeshRenderer(Device device, Mesh[] meshes) {
    _device = device;
    Init(meshes);
  }

  protected void Init(Mesh[] meshes) {
    _meshesCount = meshes.Length;
    _indexCount = new ulong[_meshesCount];
    _indexBuffers = new Vulkan.Buffer[_meshesCount];
    _indexCount = new ulong[_meshesCount];
    _vertexBuffers = new Vulkan.Buffer[_meshesCount];
    _vertexCount = new ulong[_meshesCount];
    _hasIndexBuffer = new bool[_meshesCount];
    _textureIdRefs = new Guid[_meshesCount];

    List<Task> createTasks = new();

    _meshes = meshes;
    _aabbes = new AABB[_meshesCount];

    for (int i = 0; i < meshes.Length; i++) {
      if (meshes[i].Indices.Length > 0) _hasIndexBuffer[i] = true;
      _textureIdRefs[i] = Guid.Empty;
      createTasks.Add(CreateVertexBuffer(meshes[i].Vertices, (uint)i));
      createTasks.Add(CreateIndexBuffer(meshes[i].Indices, (uint)i));

      _aabbes[i] = new();
      _aabbes[i].Update(meshes[i]);
    }

    _mergedAABB.Update(_aabbes);

    RunTasks(createTasks);
  }

  protected async void RunTasks(List<Task> createTasks) {
    await Task.WhenAll(createTasks);
    _finishedInitialization = true;
  }

  public Task Bind(VkCommandBuffer commandBuffer, uint index) {
    _device._mutex.WaitOne();
    VkBuffer[] buffers = [_vertexBuffers[index].GetBuffer()];
    ulong[] offsets = [0];
    unsafe {
      fixed (VkBuffer* buffersPtr = buffers)
      fixed (ulong* offsetsPtr = offsets) {
        vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
      }
    }

    if (_hasIndexBuffer[index]) {
      vkCmdBindIndexBuffer(commandBuffer, _indexBuffers[index].GetBuffer(), 0, VkIndexType.Uint32);
    }
    _device._mutex.ReleaseMutex();
    return Task.CompletedTask;
  }

  public Task Draw(VkCommandBuffer commandBuffer, uint index) {
    _device._mutex.WaitOne();
    if (_hasIndexBuffer[index]) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount[index], 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount[index], 1, 0, 0);
    }
    _device._mutex.ReleaseMutex();
    return Task.CompletedTask;
  }

  public void DrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, ulong offset, uint drawCount, uint stride) {
    vkCmdDrawIndexedIndirect(commandBuffer, buffer, offset, drawCount, stride);
  }

  public async void BindToTexture(
    TextureManager textureManager,
    string texturePath,
    int modelPart = 0
  ) {
    _textureIdRefs[modelPart] = textureManager.GetTextureId(texturePath);

    if (_textureIdRefs[modelPart] == Guid.Empty) {
      var texture = await Texture.LoadFromPath(_device, texturePath);
      await textureManager.AddTexture(texture);
      _textureIdRefs[modelPart] = textureManager.GetTextureId(texturePath);

      Logger.Warn($"Could not bind texture to model ({texturePath}) - no such texture in manager");
      Logger.Info($"Binding ({texturePath})");
    }
  }

  public void BindMultipleModelPartsToTexture(TextureManager textureManager, string path) {
    for (int i = 0; i < _meshesCount; i++) {
      BindToTexture(textureManager, path, i);
    }
  }

  public void BindMultipleModelPartsToTextures(
    TextureManager textureManager,
    ReadOnlySpan<string> paths
  ) {
    for (int i = 0; i < _meshesCount; i++) {
      BindToTexture(textureManager, paths[i], i);
    }
  }

  protected unsafe Task CreateVertexBuffer(Vertex[] vertices, uint index) {
    _vertexCount[index] = (ulong)vertices.Length;
    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount[index];
    ulong vertexSize = (ulong)Unsafe.SizeOf<Vertex>();

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount[index],
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffers[index] = new Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount[index],
      VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffers[index].GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  protected unsafe Task CreateIndexBuffer(uint[] indices, uint index) {
    _indexCount[index] = (ulong)indices.Length;
    if (!_hasIndexBuffer[index]) return Task.CompletedTask;
    ulong bufferSize = sizeof(uint) * _indexCount[index];
    ulong indexSize = sizeof(uint);

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount[index],
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(indices), bufferSize);

    _indexBuffers[index] = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount[index],
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffers[index].GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  public unsafe void Dispose() {
    for (int i = 0; i < _vertexBuffers.Length; i++) {
      vkQueueWaitIdle(_device.PresentQueue);
      vkDeviceWaitIdle(_device.LogicalDevice);
      _vertexBuffers[i]?.Dispose();
      if (_hasIndexBuffer[i]) {
        _indexBuffers[i]?.Dispose();
      }
    }
  }
  public int MeshsesCount => _meshesCount;
  public Mesh[] Meshes => _meshes;
  public float CalculateHeightOfAnModel() {
    var height = 0.0f;
    foreach (var m in _meshes) {
      height += m.Height;
    }
    return height;
  }
  public Guid GetTextureIdReference(int index = 0) {
    return _textureIdRefs[index];
  }
  public bool FinishedInitialization => _finishedInitialization;

  public AABB[] AABBArray {
    get {
      return _aabbes;
    }
  }

  public AABB AABB {
    get {
      if (Owner!.HasComponent<ColliderMesh>()) {
        return AABB.CalculateOnFlyWithMatrix(Owner!.GetComponent<ColliderMesh>().Mesh, Owner!.GetComponent<Transform>());
      } else {
        return _mergedAABB;
      }
    }
  }
}