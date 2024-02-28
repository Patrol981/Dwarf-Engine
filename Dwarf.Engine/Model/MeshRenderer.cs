using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Rendering;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;

namespace Dwarf.Engine;

public class MeshRenderer : Component, IRender3DElement, ICollision {
  private readonly IDevice _device = null!;
  private readonly Renderer _renderer = null!;

  private DwarfBuffer[] _vertexBuffers = [];
  private ulong[] _vertexCount = [];
  private bool[] _hasIndexBuffer = [];
  private DwarfBuffer[] _indexBuffers = [];
  private ulong[] _indexCount = [];
  private Guid[] _textureIdRefs = [];
  private AABB _mergedAABB = new();

  public MeshRenderer() { }

  public MeshRenderer(IDevice device, Renderer renderer) {
    _device = device;
    _renderer = renderer;
  }

  public MeshRenderer(IDevice device, Renderer renderer, Mesh[] meshes) {
    _device = device;
    _renderer = renderer;
    Init(meshes);
  }

  public MeshRenderer(IDevice device, Renderer renderer, Mesh[] meshes, string fileName) {
    _device = device;
    _renderer = renderer;
    FileName = fileName;
    Init(meshes);
  }

  protected void Init(Mesh[] meshes) {
    MeshsesCount = meshes.Length;
    _indexCount = new ulong[MeshsesCount];
    _indexBuffers = new DwarfBuffer[MeshsesCount];
    _indexCount = new ulong[MeshsesCount];
    _vertexBuffers = new DwarfBuffer[MeshsesCount];
    _vertexCount = new ulong[MeshsesCount];
    _hasIndexBuffer = new bool[MeshsesCount];
    _textureIdRefs = new Guid[MeshsesCount];

    List<Task> createTasks = new();

    Meshes = meshes;
    AABBArray = new AABB[MeshsesCount];

    for (int i = 0; i < meshes.Length; i++) {
      if (meshes[i].Indices.Length > 0) _hasIndexBuffer[i] = true;
      _textureIdRefs[i] = Guid.Empty;
      createTasks.Add(CreateVertexBuffer(meshes[i].Vertices, (uint)i));
      createTasks.Add(CreateIndexBuffer(meshes[i].Indices, (uint)i));

      AABBArray[i] = new();
      AABBArray[i].Update(meshes[i]);
    }

    _mergedAABB.Update(AABBArray);

    RunTasks(createTasks);
  }

  protected async void RunTasks(List<Task> createTasks) {
    await Task.WhenAll(createTasks);
    FinishedInitialization = true;
  }

  public Task Bind(IntPtr commandBuffer, uint index) {
    _renderer.CommandList.BindVertex(commandBuffer, index, _vertexBuffers, [0]);

    if (_hasIndexBuffer[index]) {
      _renderer.CommandList.BindIndex(commandBuffer, index, _indexBuffers);
    }

    return Task.CompletedTask;
  }

  public Task Draw(IntPtr commandBuffer, uint index) {
    if (_hasIndexBuffer[index]) {
      _renderer.CommandList.DrawIndexed(commandBuffer, index, _indexCount, 1, 0, 0, 0);
    } else {
      _renderer.CommandList.Draw(commandBuffer, index, _vertexCount, 1, 0, 0);
    }
    return Task.CompletedTask;
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
    for (int i = 0; i < MeshsesCount; i++) {
      BindToTexture(textureManager, path, i);
    }
  }

  public void BindMultipleModelPartsToTextures(
    TextureManager textureManager,
    ReadOnlySpan<string> paths
  ) {
    for (int i = 0; i < MeshsesCount; i++) {
      BindToTexture(textureManager, paths[i], i);
    }
  }

  protected unsafe Task CreateVertexBuffer(Vertex[] vertices, uint index) {
    _vertexCount[index] = (ulong)vertices.Length;
    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount[index];
    ulong vertexSize = (ulong)Unsafe.SizeOf<Vertex>();

    var stagingBuffer = new DwarfBuffer(
      _device,
      vertexSize,
      _vertexCount[index],
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffers[index] = new DwarfBuffer(
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

    var stagingBuffer = new DwarfBuffer(
      _device,
      indexSize,
      _indexCount[index],
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(indices), bufferSize);

    _indexBuffers[index] = new DwarfBuffer(
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
  public int MeshsesCount { get; private set; } = 0;
  public Mesh[] Meshes { get; private set; } = [];
  public string FileName { get; } = "";
  public int TextureFlipped { get; set; } = 1;
  public float CalculateHeightOfAnModel() {
    var height = 0.0f;
    foreach (var m in Meshes) {
      height += m.Height;
    }
    return height;
  }
  public Guid GetTextureIdReference(int index = 0) {
    return _textureIdRefs[index];
  }
  public bool FinishedInitialization { get; private set; } = false;

  public AABB[] AABBArray { get; private set; } = [];

  public AABB AABB {
    get {
      return Owner!.HasComponent<ColliderMesh>()
        ? AABB.CalculateOnFlyWithMatrix(Owner!.GetComponent<ColliderMesh>().Mesh, Owner!.GetComponent<Transform>())
        : _mergedAABB;
    }
  }
}