
using System.Numerics;
using System.Runtime.CompilerServices;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Physics;
public class ColliderMesh : Component, IDebugRender3DObject {
  private readonly IDevice _device = null!;
  private readonly VmaAllocator _vmaAllocator = VmaAllocator.Null;

  private DwarfBuffer _vertexBuffer = null!;
  private DwarfBuffer _indexBuffer = null!;
  private ulong _vertexCount = 0;
  private ulong _indexCount = 0;
  private readonly bool _hasIndexBuffer = false;

  public ColliderMesh() { }

  public ColliderMesh(VmaAllocator vmaAllocator, IDevice device, Mesh mesh) {
    _vmaAllocator = vmaAllocator;
    _device = device;
    Mesh = mesh;

    if (Mesh.Indices.Length > 0) _hasIndexBuffer = true;

    Init();
  }

  public ColliderMesh(VmaAllocator vmaAllocator, IDevice device, AABB aabb) {
    _device = device;
    _vmaAllocator = vmaAllocator;

    Mesh = CreateMeshOutOfAABB(vmaAllocator, device, aabb);

    if (Mesh.Indices.Length > 0) _hasIndexBuffer = true;
    Init();
  }

  public async void Init() {
    await CreateVertexBuffer(Mesh.Vertices);
    await CreateIndexBuffer(Mesh.Indices);
    FinishedInitialization = true;
  }

  public void Bind(VkCommandBuffer commandBuffer) {
    throw new NotImplementedException();
  }

  public unsafe Task Bind(IntPtr commandBuffer, uint index = 0) {
    // _device._mutex.WaitOne();
    VkBuffer[] buffers = [_vertexBuffer.GetBuffer()];
    ulong[] offsets = [0];
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }

    if (_hasIndexBuffer) {
      vkCmdBindIndexBuffer(commandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint32);
    }
    // _device._mutex.ReleaseMutex();
    return Task.CompletedTask;
  }

  public void BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout) {
    throw new Exception("Colldier mesh should not have textures");
  }

  public void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();
    _vertexBuffer?.Dispose();
    if (_hasIndexBuffer) {
      _indexBuffer?.Dispose();
    }
  }

  public Task Draw(IntPtr commandBuffer, uint index = 0, uint firstInstance = 0) {
    // _device._mutex.WaitOne();
    if (_hasIndexBuffer) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount, 1, 0, 0, firstInstance);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount, 1, 0, firstInstance);
    }
    // _device._mutex.ReleaseMutex();
    return Task.CompletedTask;
  }

  private unsafe Task CreateVertexBuffer(Vertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;
    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount;
    ulong vertexSize = (ulong)Unsafe.SizeOf<Vertex>();

    var stagingBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(bufferSize);
    fixed (Vertex* verticesPtr = vertices) {
      stagingBuffer.WriteToBuffer((nint)verticesPtr);
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    Application.Instance.Mutex.WaitOne();
    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffer.GetBuffer(), bufferSize);
    Application.Instance.Mutex.ReleaseMutex();
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  private unsafe Task CreateIndexBuffer(uint[] indices) {
    _indexCount = (ulong)indices.Length;
    if (!_hasIndexBuffer) return Task.CompletedTask;
    ulong bufferSize = sizeof(uint) * _indexCount;
    ulong indexSize = sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      indexSize,
      _indexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(bufferSize);
    fixed (uint* indicesPtr = indices) {
      stagingBuffer.WriteToBuffer((nint)indicesPtr, bufferSize);
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(indices), bufferSize);

    _indexBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      indexSize,
      _indexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffer.GetBuffer(), bufferSize);

    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  private static Mesh CreateMeshOutOfAABB(VmaAllocator vmaAllocator, IDevice device, AABB aabb) {
    Vector3[] normals = [
      new(-1, -1, -1),
      new(1, -1, -1),
      new(1, 1, -1),
      new(-1, 1, -1),

      new(-1, -1, 1),
      new(1, -1, 1),
      new(1, 1, 1),
      new(-1, 1, 1),
    ];

    Vertex[] vertices =
    [
      new Vertex {
        Position = new(aabb.Min.X, aabb.Min.Y, aabb.Min.Z),
        Color = new(1, 1, 1),
        Normal = normals[0],
      },
      new Vertex {
        Position = new(aabb.Max.X, aabb.Min.Y, aabb.Min.Z),
        Color = new(1, 1, 1),
        Normal = normals[1],
      },
      new Vertex {
        Position = new(aabb.Max.X, aabb.Max.Y, aabb.Min.Z),
        Color = new(1, 1, 1),
        Normal = normals[2],
      },
      new Vertex {
        Position = new(aabb.Min.X, aabb.Max.Y, aabb.Min.Z),
        Color = new(1, 1, 1),
        Normal = normals[3],
      },

      new Vertex {
        Position = new(aabb.Min.X, aabb.Min.Y, aabb.Max.Z),
        Color = new(1, 1, 1),
        Normal = normals[4],
      },
      new Vertex {
        Position = new(aabb.Max.X, aabb.Min.Y, aabb.Max.Z),
        Color = new(1, 1, 1),
        Normal = normals[5],
      },
      new Vertex {
        Position = new(aabb.Max.X, aabb.Max.Y, aabb.Max.Z),
        Color = new(1, 1, 1),
        Normal = normals[6],
      },
      new Vertex {
        Position = new(aabb.Min.X, aabb.Max.Y, aabb.Max.Z),
        Color = new(1, 1, 1),
        Normal = normals[7],
      },
    ];

    uint[] indices = [
      // Front face
      0,
      3,
      2,
      2,
      1,
      0,

      // Back face
      4,
      5,
      6,
      6,
      7,
      4,

      // Left face
      0,
      4,
      7,
      7,
      3,
      0,

      // Right face
      1,
      2,
      6,
      6,
      5,
      1,

      // Top face
      3,
      7,
      6,
      6,
      2,
      3,

      // Bottom face
      0,
      1,
      5,
      5,
      4,
      0
    ];

    var mesh = new Mesh(vmaAllocator, device) {
      Vertices = vertices,
      Indices = indices,
      IndexCount = (ulong)indices.Length,
      VertexCount = (ulong)vertices.Length,
    };

    return mesh;
  }

  public bool UsesTexture => false;
  public bool UsesLight {
    get { return false; }
    set { }
  }

  public int MeshsesCount => 1;

  public bool FinishedInitialization { get; private set; } = false;

  public Mesh Mesh { get; } = null!;

  public bool Enabled { get; private set; } = true;
  public void Enable() {
    Enabled = true;
  }
  public void Disable() {
    Enabled = false;
  }
}
