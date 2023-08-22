using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine;

public class Model : Component, IRender3DElement {
  internal class ModelLoader {
    private readonly Model _model;
    private readonly int _index;
    private readonly Mesh _mesh;

    public ModelLoader(Model model, int index, Mesh mesh) {
      _model = model;
      _index = index;
      _mesh = mesh;
    }

    public async void Proceed() {
      _model._device._mutex.WaitOne();
      vkQueueWaitIdle(_model._device.GraphicsQueue);
      vkDeviceWaitIdle(_model._device.LogicalDevice);
      var tasks = new List<Task>() {
        CreateVertexBuffer(),
        CreateIndexBuffer(),
      };

      await Task.WhenAll(tasks);
      _model._device._mutex.ReleaseMutex();
    }

    public Task CreateVertexBuffer() {
      // vkQueueWaitIdle(_model._device.GraphicsQueue);
      // vkDeviceWaitIdle(_model._device.LogicalDevice);
      _model.CreateVertexBuffer(_mesh.Vertices, (uint)_index);
      return Task.CompletedTask;
    }

    public Task CreateIndexBuffer() {
      // vkQueueWaitIdle(_model._device.GraphicsQueue);
      // vkDeviceWaitIdle(_model._device.LogicalDevice);
      _model.CreateIndexBuffer(_mesh.Indices, (uint)_index);
      return Task.CompletedTask;
    }
  }

  private readonly Device _device = null!;

  private Dwarf.Vulkan.Buffer[] _vertexBuffers = new Vulkan.Buffer[0];
  private ulong[] _vertexCount = new ulong[0];
  private bool[] _hasIndexBuffer = new bool[0];
  private Dwarf.Vulkan.Buffer[] _indexBuffers = new Vulkan.Buffer[0];
  private ulong[] _indexCount = new ulong[0];
  private int _meshesCount = 0;
  private bool _usesTexture = false;
  private Guid[] _textureIdRefs = new Guid[0];

  private Mesh[] _meshes;

  private bool _finishedInitialization = false;
  private bool _usesLight = true;

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

    // List<Thread> threads = new();
    // List<ModelLoader> loaders = new();

    List<Task> createTasks = new();

    _meshes = meshes;

    for (int i = 0; i < meshes.Length; i++) {
      if (meshes[i].Indices.Length > 0) _hasIndexBuffer[i] = true;
      _textureIdRefs[i] = Guid.Empty;
      // loaders.Add(new ModelLoader(this, i, meshes[i]));
      // threads.Add(new(new ThreadStart(loaders[i].CreateVertexBuffer)));
      // threads.Add(new(new ThreadStart(loaders[i].CreateIndexBuffer)));
      // threads.Add(new(new ThreadStart(loaders[i].Proceed)));
      createTasks.Add(CreateVertexBuffer(meshes[i].Vertices, (uint)i));
      createTasks.Add(CreateIndexBuffer(meshes[i].Indices, (uint)i));
    }

    Init(createTasks);
  }

  private async void Init(List<Task> createTasks) {
    var startTime = DateTime.Now;
    await Task.WhenAll(createTasks);
    _finishedInitialization = true;
    var endTime = DateTime.Now;
    // Logger.Warn($"[Load time]: {(endTime - startTime).TotalMilliseconds}");
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
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount[index], 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount[index], 1, 0, 0);
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
  public bool UsesTexture => _usesTexture;
  public bool UsesLight {
    get { return _usesLight; }
    set { _usesLight = value; }
  }
  public Guid GetTextureIdReference(int index = 0) {
    return _textureIdRefs[index];
  }
  public bool FinishedInitialization => _finishedInitialization;
}