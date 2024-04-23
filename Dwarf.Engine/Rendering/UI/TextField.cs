using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.UI;
public class TextField : Component, IUIElement {
  private readonly Application _app = null!;

  private readonly VulkanDevice _device = null!;
  private DwarfBuffer _vertexBuffer = null!;
  private DwarfBuffer _indexBuffer = null!;
  private Mesh _textMesh = null!;
  private Guid _textAtlasId = Guid.Empty;
  private ulong _vertexCount = 0;
  private ulong _indexCount = 0;
  private bool _hasIndexBuffer = false;

  // debug
  private readonly int _numOfRows = 11;
  private readonly Dictionary<char, Vector2> _charactersOnAtlas = new();

  private Vector2 _startPos = Vector2.Zero;
  private Vector2 _startPosUpdated = Vector2.Zero;

  readonly float _cursorX = 96.0f; // size of an glyph
  // readonly float _cursorY = 96.0f; // size of an glyph

  readonly float _glyphOffset;

  public TextField() { }

  public TextField(Application app) {
    _app = app;
    _device = _app.Device;

    _glyphOffset = _cursorX / 1024;
  }

  public void Init() {
    _textMesh = new();

    // setup chars mappings
    int currX = 0;
    int currY = 0;
    for (char c = (char)32; c < 128; c++) {
      _charactersOnAtlas.Add(c, new Vector2(currX, currY));
      currX++;
      if (currX > _numOfRows - 1) {
        currX = 0;
        currY++;
      }
    }

    if (_textMesh.Indices.Length > 0) _hasIndexBuffer = true;
    CreateQuads();
    CreateVertexBuffer(_textMesh.Vertices);

    var pos = Owner!.GetComponent<RectTransform>().Position;
    _startPos = new(pos.X, pos.Y);
    _startPosUpdated = _startPos;
  }

  public Task Draw(IntPtr commandBuffer, uint index = 0) {
    if (_hasIndexBuffer) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount, 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount, 1, 0, 0);
    }
    return Task.CompletedTask;
  }

  public void DrawText(string text) {
    if (text == Text) return;
    Text = text;
    CreateQuads();
    RecreateBuffers();
    Owner!.TryGetComponent<RectTransform>()?.SetRequireState();
  }

  public void Update() {
    CheckBuffers(_textMesh.Vertices);
  }

  private void CreateQuads() {
    _textMesh = new();

    var pos = Owner!.GetComponent<RectTransform>().Position;
    float offsetMeshX = pos.X;
    float offsetMeshY = pos.Y;

    for (int i = 0; i < Text.Length; i++) {
      var tempMesh = new Mesh();
      var targetChar = _charactersOnAtlas[Text[i]];
      tempMesh.Vertices = new Vertex[6];

      var uX = ((targetChar.X * 96.0f) / 1024.0f);
      var uY = 1.0f - ((targetChar.Y * 96.0f) / 1024.0f);

      var uMX = (((targetChar.X * 96.0f) + 96.0f) / 1024.0f);
      var uMY = 1.0f - (((targetChar.Y * 96.0f) + 96.0f) / 1024.0f);

      var tmpMaster = _textMesh.Vertices.ToList();

      tempMesh.Vertices[0].Position = new Vector3(0.0f - offsetMeshX, 0.0f + offsetMeshY, 0.0f);
      tempMesh.Vertices[1].Position = new Vector3(0.0f - offsetMeshX, _glyphOffset + offsetMeshY, 0.0f);
      tempMesh.Vertices[2].Position = new Vector3(_glyphOffset - offsetMeshX, 0.0f + offsetMeshY, 0.0f);

      tempMesh.Vertices[3].Position = new Vector3(_glyphOffset - offsetMeshX, 0.0f + offsetMeshY, 0.0f);
      tempMesh.Vertices[4].Position = new Vector3(0f - offsetMeshX, _glyphOffset + offsetMeshY, 0.0f);
      tempMesh.Vertices[5].Position = new Vector3(_glyphOffset - offsetMeshX, _glyphOffset + offsetMeshY, 0.0f);

      tempMesh.Vertices[0].Uv = new Vector2(uMX, uY);
      tempMesh.Vertices[1].Uv = new Vector2(uMX, uMY);
      tempMesh.Vertices[2].Uv = new Vector2(uX, uY);

      tempMesh.Vertices[3].Uv = new Vector2(uX, uY);
      tempMesh.Vertices[4].Uv = new Vector2(uMX, uMY);
      tempMesh.Vertices[5].Uv = new Vector2(uX, uMY);

      tempMesh.Vertices[0].Color = new Vector3(1, 1, 1);
      tempMesh.Vertices[1].Color = new Vector3(1, 1, 1);
      tempMesh.Vertices[2].Color = new Vector3(1, 1, 1);
      tempMesh.Vertices[3].Color = new Vector3(1, 1, 1);
      tempMesh.Vertices[4].Color = new Vector3(1, 1, 1);
      tempMesh.Vertices[5].Color = new Vector3(1, 1, 1);

      tmpMaster.AddRange(tempMesh.Vertices);
      _textMesh.Vertices = tmpMaster.ToArray();

      offsetMeshX += _glyphOffset;
    }
  }

  private void CheckBuffers(Vertex[] vertices) {
    if (_vertexCount == (ulong)vertices.Length) return;

    // _device.WaitDevice();
    Dispose();
    CreateVertexBuffer(vertices);
  }

  private void RecreateBuffers() {
    // _device.WaitDevice();
    Dispose();
    CreateVertexBuffer(_textMesh.Vertices);
  }

  private void AddVertices(ref Mesh mesh, Vector2 pos, Vector2 maxPos, Vector2 uvPos, Vector2 uvMaxPos) {
    mesh.Vertices = new Vertex[6];

    mesh.Vertices[0] = new Vertex {
      Position = new Vector3(pos.X, pos.Y, 0),
      Uv = new Vector2(uvPos.X, uvPos.Y),
      Color = new Vector3(1, 1, 1)
    };

    mesh.Vertices[1] = new Vertex {
      Position = new Vector3(pos.X, maxPos.Y, 0),
      Uv = new Vector2(uvPos.X, uvMaxPos.Y),
      Color = new Vector3(1, 1, 1)
    };

    mesh.Vertices[2] = new Vertex {
      Position = new Vector3(maxPos.X, maxPos.Y, 0),
      Uv = new Vector2(uvMaxPos.X, uvMaxPos.Y),
      Color = new Vector3(1, 1, 1)
    };

    mesh.Vertices[3] = new Vertex {
      Position = new Vector3(maxPos.X, maxPos.Y, 0),
      Uv = new Vector2(uvMaxPos.X, uvMaxPos.Y),
      Color = new Vector3(1, 1, 1)
    };

    mesh.Vertices[4] = new Vertex {
      Position = new Vector3(maxPos.X, pos.Y, 0),
      Uv = new Vector2(uvMaxPos.X, uvPos.Y),
      Color = new Vector3(1, 1, 1)
    };

    mesh.Vertices[5] = new Vertex {
      Position = new Vector3(pos.X, pos.Y, 0),
      Uv = new Vector2(uvPos.X, uvPos.Y),
      Color = new Vector3(1, 1, 1)
    };
  }

  private void CreateVertexBuffer(Vertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;

    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount;
    ulong vertexSize = ((ulong)Unsafe.SizeOf<Vertex>());

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
        stagingBuffer.WriteToBuffer((nint)verticesPtr, bufferSize);
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

  private unsafe void CreateIndexBuffer(uint[] indices) {
    _indexCount = (ulong)indices.Length;
    if (!_hasIndexBuffer) return;

    ulong bufferSize = (ulong)sizeof(uint) * _indexCount;
    ulong indexSize = (ulong)sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _device,
      indexSize,
      _indexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    fixed (uint* indicesPtr = indices) {
      stagingBuffer.WriteToBuffer((nint)indicesPtr, bufferSize);
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(indices), bufferSize);
    stagingBuffer.Unmap();

    _indexBuffer = new DwarfBuffer(
      _device,
      indexSize,
      _indexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  public void BindToTexture(
    TextureManager textureManager,
    string texturePath
  ) {
    _textAtlasId = textureManager.GetTextureId(texturePath);

    if (_textAtlasId == Guid.Empty) {
      Logger.Warn($"Could not bind texture to text ({texturePath}) - no such texture in manager");
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

  public void Bind(VkCommandBuffer commandBuffer) {
    throw new NotImplementedException();
  }

  public unsafe Task Bind(IntPtr commandBuffer, uint index = 0) {
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffer.GetBuffer() };
    ulong[] offsets = { 0 };
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }
    return Task.CompletedTask;
  }

  public void Dispose() {
    _vertexBuffer?.Dispose();
    if (_hasIndexBuffer) {
      _indexBuffer?.Dispose();
    }
  }

  public void SetText(string text) {
    Text = text;
  }

  public string Text { get; private set; } = "Enter text...";

  public Guid GetTextureIdReference() {
    return _textAtlasId;
  }
}
