using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
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
  private readonly IDevice _device = null!;

  private Vulkan.DwarfBuffer[] _vertexBuffers = [];
  private ulong[] _vertexCount = [];
  private bool[] _hasIndexBuffer = [];
  private Vulkan.DwarfBuffer[] _indexBuffers = [];
  private ulong[] _indexCount = [];
  private int _meshesCount = 0;
  private Guid[] _textureIdRefs = [];

  private Mesh[] _meshes = [];
  private AABB[] _aabbes = [];
  private AABB _mergedAABB = new();

  private bool _finishedInitialization = false;

  public MeshRenderer() { }

  public MeshRenderer(IDevice device) {
    _device = device;
  }

  public MeshRenderer(IDevice device, Mesh[] meshes) {
    _device = device;
    Init(meshes);
  }

  protected void Init(Mesh[] meshes) {
    _meshesCount = meshes.Length;
    _indexCount = new ulong[_meshesCount];
    _indexBuffers = new Vulkan.DwarfBuffer[_meshesCount];
    _indexCount = new ulong[_meshesCount];
    _vertexBuffers = new Vulkan.DwarfBuffer[_meshesCount];
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

  public Task Bind(IntPtr commandBuffer, uint index) {
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
    return Task.CompletedTask;
  }

  public Task Draw(IntPtr commandBuffer, uint index) {
    if (_hasIndexBuffer[index]) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount[index], 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount[index], 1, 0, 0);
    }
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
      var texture = await TextureLoader.LoadFromPath(_device, texturePath);
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

    var stagingBuffer = new Vulkan.DwarfBuffer(
      _device,
      vertexSize,
      _vertexCount[index],
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffers[index] = new Vulkan.DwarfBuffer(
      _device,
      vertexSize,
      _vertexCount[index],
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
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

    var stagingBuffer = new Vulkan.DwarfBuffer(
      _device,
      indexSize,
      _indexCount[index],
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(indices), bufferSize);

    _indexBuffers[index] = new Vulkan.DwarfBuffer(
      _device,
      indexSize,
      _indexCount[index],
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffers[index].GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  public unsafe void Dispose() {
    for (int i = 0; i < _vertexBuffers.Length; i++) {
      _device.WaitQueue();
      _device.WaitDevice();
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