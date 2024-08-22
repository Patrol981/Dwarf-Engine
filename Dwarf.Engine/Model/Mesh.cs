using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;

namespace Dwarf;

public class Mesh : IDisposable {
  private readonly IDevice _device;

  public Vertex[] Vertices = [];
  public uint[] Indices = [];

  public DwarfBuffer? VertexBuffer { get; private set; }
  public DwarfBuffer? IndexBuffer { get; private set; }

  public ulong VertexCount = 0;
  public ulong IndexCount = 0;

  public bool HasIndexBuffer => IndexCount > 0;

  public Guid TextureIdReference = Guid.Empty;
  public string TextureName = string.Empty;

  public Mesh(IDevice device) {
    _device = device;
  }

  public void BuildDescriptors() {

  }

  public unsafe Task CreateVertexBuffer() {
    VertexCount = (ulong)Vertices.Length;
    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * VertexCount;
    ulong vertexSize = (ulong)Unsafe.SizeOf<Vertex>();

    var stagingBuffer = new DwarfBuffer(
      _device,
      vertexSize,
      VertexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(bufferSize);
    fixed (Vertex* verticesPtr = Vertices) {
      stagingBuffer.WriteToBuffer((nint)verticesPtr, bufferSize);
    }

    VertexBuffer = new DwarfBuffer(
      _device,
      vertexSize,
      VertexCount,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), VertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  public unsafe Task CreateIndexBuffer() {
    IndexCount = (ulong)Indices.Length;
    if (!HasIndexBuffer) return Task.CompletedTask;

    ulong bufferSize = sizeof(uint) * IndexCount;
    ulong indexSize = sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _device,
      indexSize,
      IndexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(bufferSize);
    fixed (uint* indicesPtr = Indices) {
      stagingBuffer.WriteToBuffer((nint)indicesPtr, bufferSize);
    }

    IndexBuffer = new DwarfBuffer(
      _device,
      indexSize,
      IndexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), IndexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  public async void BindToTexture(TextureManager textureManager, string texturePath) {
    TextureIdReference = textureManager.GetTextureId(texturePath);

    if (TextureIdReference == Guid.Empty) {
      var texture = await TextureLoader.LoadFromPath(_device, texturePath);
      textureManager.AddTexture(texture);
      TextureIdReference = textureManager.GetTextureId(texturePath);

      Logger.Warn($"Could not bind texture to model ({texturePath}) - no such texture in manager");
      Logger.Info($"Binding ({texturePath})");
    }
  }

  public void BindToTexture(TextureManager textureManager, Guid textureId) {
    TextureIdReference = textureId;

    if (TextureIdReference == Guid.Empty) {
      throw new ArgumentException("Guid is empty!");
    }
  }

  public float Height {
    get {
      double minY = double.MaxValue;
      double maxY = double.MinValue;

      foreach (var v in Vertices) {
        if (v.Position.Y < minY)
          minY = v.Position.Y;
        if (v.Position.Y > maxY)
          maxY = v.Position.Y;
      }

      return (float)(maxY - minY);
    }
  }

  public void Dispose() {
    VertexBuffer?.Dispose();
    if (HasIndexBuffer) {
      IndexBuffer?.Dispose();
    }
  }
}