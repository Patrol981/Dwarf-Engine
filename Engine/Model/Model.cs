using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Rendering;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Model : Component, IRender3DElement, ICollision {
  private readonly Device _device = null!;

  private Dwarf.Vulkan.Buffer[] _vertexBuffers = new Vulkan.Buffer[0];
  private ulong[] _vertexCount = new ulong[0];
  private bool[] _hasIndexBuffer = new bool[0];
  private Dwarf.Vulkan.Buffer[] _indexBuffers = new Vulkan.Buffer[0];
  private ulong[] _indexCount = new ulong[0];
  private int _meshesCount = 0;
  private Guid[] _textureIdRefs = new Guid[0];

  private Mesh[] _meshes;
  private AABB[] _aabbes;
  private AABB _mergedAABB = new();

  private bool _finishedInitialization = false;

  public Model() { }

  public Model(Device device) {
    _device = device;
  }

  public Model(Device device, Mesh[] meshes) {
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

  public unsafe void BindDescriptorSets(VkDescriptorSet[] descriptorSets, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout) {
    vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        pipelineLayout,
        0,
        descriptorSets
      );
  }

  public unsafe Task BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout) {
    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      0,
      1,
      &textureSet,
      0,
      null
    );
    return Task.CompletedTask;
  }

  public Task Bind(VkCommandBuffer commandBuffer, uint index) {
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffers[index].GetBuffer() };
    ulong[] offsets = { 0 };
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

  public Task Draw(VkCommandBuffer commandBuffer, uint index) {
    if (_hasIndexBuffer[index]) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount[index], 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount[index], 1, 0, 0);
    }
    // vkCmdDrawIndirect(commandBuffer, );
    return Task.CompletedTask;
  }

  public void BindToTexture(
    TextureManager textureManager,
    string texturePath,
    bool useLocalPath = false,
    int modelPart = 0
  ) {
    if (useLocalPath) {
      _textureIdRefs[modelPart] = textureManager.GetTextureId($"./Textures/{texturePath}");
    } else {
      _textureIdRefs[modelPart] = textureManager.GetTextureId(texturePath);
    }

    if (_textureIdRefs[modelPart] == Guid.Empty) {
      Logger.Warn($"Could not bind texture to model ({texturePath}) - no such texture in manager");
    }
  }

  public void BindMultipleModelPartsToTexture(
    TextureManager textureManager,
    string path
  ) {
    for (int i = 0; i < _meshesCount; i++) {
      BindToTexture(textureManager, path, false, i);
    }
  }

  public void BindMultipleModelPartsToTextures(
    TextureManager textureManager,
    ReadOnlySpan<string> paths,
    bool useLocalPath = false
  ) {
    for (int i = 0; i < _meshesCount; i++) {
      BindToTexture(textureManager, paths[i], useLocalPath, i);
    }
  }

  protected unsafe Task CreateVertexBuffer(Vertex[] vertices, uint index) {
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

    // stagingBuffer.Map(bufferSize);
    // stagingBuffer.WriteToBuffer(Utils.ToIntPtr(vertices), bufferSize);

    // _device._mutex.WaitOne();
    // vkDeviceWaitIdle(_device.LogicalDevice);
    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(vertices), bufferSize);
    // _device._mutex.ReleaseMutex();

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
    ulong bufferSize = (ulong)sizeof(uint) * _indexCount[index];
    ulong indexSize = (ulong)sizeof(uint);

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount[index],
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    // vkDeviceWaitIdle(_device.LogicalDevice);
    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(indices), bufferSize);
    //stagingBuffer.Unmap();

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
        // return AABB.CalculateOnFly(Owner!.GetComponent<ColliderMesh>().Mesh);
        return AABB.CalculateOnFlyWithMatrix(Owner!.GetComponent<ColliderMesh>().Mesh, Owner!.GetComponent<Transform>());
      } else {
        // Logger.Warn($"{Owner!.Name} is using merged AABB");
        return _mergedAABB;
      }
    }
  }
}