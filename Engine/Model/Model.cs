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
  public bool UsesLight = true;

  private readonly Device _device = null!;

  private Dwarf.Vulkan.Buffer[] _vertexBuffers = new Vulkan.Buffer[0];
  private ulong[] _vertexCount = new ulong[0];
  private bool[] _hasIndexBuffer = new bool[0];
  private Dwarf.Vulkan.Buffer[] _indexBuffers = new Vulkan.Buffer[0];
  private ulong[] _indexCount = new ulong[0];
  private int _meshesCount = 0;
  private bool _usesTexture = false;
  private Guid[] _textureIdRefs = new Guid[0];

  public Model() { }

  public Model(Device device, Mesh[] meshes) {
    _device = device;
    _meshesCount = meshes.Length;
    _indexCount = new ulong[_meshesCount];
    _indexBuffers = new Vulkan.Buffer[_meshesCount];
    _indexCount = new ulong[_meshesCount];
    _vertexBuffers = new Vulkan.Buffer[_meshesCount];
    _vertexCount = new ulong[_meshesCount];
    _hasIndexBuffer = new bool[_meshesCount];
    _textureIdRefs = new Guid[_meshesCount];
    for (uint i = 0; i < meshes.Length; i++) {
      if (meshes[i].Indices.Length > 0) _hasIndexBuffer[i] = true;
      _textureIdRefs[i] = Guid.Empty;
      CreateVertexBuffer(meshes[i].Vertices, i);
      CreateIndexBuffer(meshes[i].Indices, i);
    }
  }

  public unsafe void BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout) {
    vkCmdBindDescriptorSets(
     frameInfo.CommandBuffer,
     VkPipelineBindPoint.Graphics,
     pipelineLayout,
     2,
     1,
     &textureSet,
     0,
     null
   );
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

    if (_textureIdRefs[modelPart] != Guid.Empty) {
      _usesTexture = true;
    } else {
      Logger.Warn($"Could not bind texture to model ({texturePath}) - no such texture in manager");
    }
  }

  public void BindMultipleModelPartsToTextures(
    TextureManager textureManager,
    ReadOnlySpan<string> paths,
    bool useLocalPath = false
  ) {
    for (int i = 0; i < paths.Length; i++) {
      BindToTexture(textureManager, paths[i], useLocalPath, i);
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

    _indexBuffers[index] = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount[index],
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffers[index].GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
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
  public bool UsesTexture => _usesTexture;
  public Guid GetTextureIdReference(int index = 0) {
    return _textureIdRefs[index];
  }
}