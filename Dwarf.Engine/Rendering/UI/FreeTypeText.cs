using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.UI;
public class FreeTypeText : Component, IUIElement {
  private readonly VulkanDevice _device;
  private readonly FreeType _ft;

  private readonly string _text = string.Empty;
  private Vector2 _pos = Vector2.Zero;
  private readonly Mesh _mesh = new() {
    Vertices = [],
  };
  private readonly Dictionary<char, Guid> _ids = [];

  private ulong _vertexCount = 0;
  private DwarfBuffer _vertexBuffer = null!;

  public FreeTypeText() {
    _device = null!;
    _ft = null!;
  }

  public FreeTypeText(VulkanDevice device, FreeType ft, string text, TextureManager textureManager) {
    _device = device;
    _ft = ft;
    _text = text;

    BindToTexture(textureManager);
    // BindToTexture(Application.Instance.TextureManager);
  }

  public void Update() {
    Logger.Info("Update");
    RecreateBuffers();
  }

  public Guid GetTextureIdReference() {
    return _ids.First().Value;
  }

  public void DrawText(string text) {
    // _text = text;
    CreateGeometry();
    RecreateBuffers();
  }

  public unsafe Task Bind(IntPtr commandBuffer, uint index) {
    VkBuffer[] buffers = [_vertexBuffer.GetBuffer()];
    ulong[] offsets = [0];
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }
    return Task.CompletedTask;
  }

  public Task Draw(IntPtr commandBuffer, uint index = 0) {
    vkCmdDraw(commandBuffer, (uint)_vertexCount, 1, 0, 0);
    return Task.CompletedTask;
  }

  public void BindToTexture(TextureManager textureManager) {
    foreach (var c in _text) {
      var texId = textureManager.GetTextureId(c.ToString());
      if (texId == Guid.Empty) {
        Logger.Warn($"Could not bind texture to text ({c}) - no such texture in manager");
        continue;
      }

      _ids.TryAdd(c, texId);
    }
  }

  private void CreateGeometry() {
    var scale = Owner!.GetComponent<Transform>().Scale;

    var tempPos = _pos;
    var tempMesh = _mesh.Vertices.ToList();

    foreach (var c in _text) {
      var character = _ft.Characters[c];

      float xPos = tempPos.X + character.Bearing.X * scale.X;
      float yPos = tempPos.Y - (character.Size.Y - character.Bearing.Y) * scale.Y;

      float w = character.Size.X * scale.X;
      float h = character.Size.Y * scale.Y;

      var mesh = new Mesh {
        Vertices = new Vertex[6]
      };

      mesh.Vertices[0] = new Vertex {
        Position = new(xPos, yPos + h, 0),
        Uv = new(0.0f, 0.0f),
        Color = Vector3.One
      };
      mesh.Vertices[1] = new Vertex {
        Position = new(xPos, yPos, 0),
        Uv = new(0.0f, 1.0f),
        Color = Vector3.One
      };
      mesh.Vertices[2] = new Vertex {
        Position = new(xPos + w, yPos, 0),
        Uv = new(1.0f, 1.0f),
        Color = Vector3.One
      };

      mesh.Vertices[3] = new Vertex {
        Position = new(xPos, yPos + h, 0),
        Uv = new(0.0f, 0.0f),
        Color = Vector3.One
      };
      mesh.Vertices[4] = new Vertex {
        Position = new(xPos + w, yPos, 0),
        Uv = new(1.0f, 1.0f),
        Color = Vector3.One
      };
      mesh.Vertices[5] = new Vertex {
        Position = new(xPos + w, yPos + h, 0),
        Uv = new(1.0f, 0.0f),
        Color = Vector3.One
      };

      tempPos.X += (character.Advance >> 6) * scale.X;

      tempMesh.AddRange(mesh.Vertices);
    }

    _mesh.Vertices = [.. tempMesh];
  }

  private void RecreateBuffers() {
    // _device.WaitDevice();
    Dispose();
    CreateVertexBuffer(_mesh.Vertices);
  }

  private void CreateVertexBuffer(Vertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;

    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount;
    ulong vertexSize = (ulong)Unsafe.SizeOf<Vertex>();

    var stagingBuffer = new DwarfBuffer(
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    unsafe {
      fixed (Vertex* verticesPtr = vertices) {
        stagingBuffer.WriteToBuffer((nint)verticesPtr);
      }
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new DwarfBuffer(
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  public void Dispose() {
    _vertexBuffer?.Dispose();
  }
}