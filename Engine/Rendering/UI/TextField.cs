using System.Runtime.CompilerServices;

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering.UI.FontReader;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace DwarfEngine.Engine.Rendering.UI;
public class TextField : Component, IDisposable {
  private readonly Application _app = null!;

  private readonly Device _device;
  private Dwarf.Vulkan.Buffer _vertexBuffer = null!;
  private Dwarf.Vulkan.Buffer _indexBuffer = null!;
  private Mesh _textMesh = null!;
  private Guid _textAtlasId = Guid.Empty;
  private ulong _vertexCount = 0;
  private ulong _indexCount = 0;
  private bool _hasIndexBuffer = false;

  private string _text = string.Empty;
  private float _fontSize = 1;
  private float _maxLineSize = 1;
  private int _numberOfLines = 1;
  private bool _isCentered = false;
  private List<Line> _lines = new();

  public TextField() { }

  public TextField(Application app, string text) {
    _app = app;
    _text = text;
    _device = _app.Device;
  }

  public void RenderText() {
    UpdatePosition();
  }

  public void Init(FontFile fontFile) {
    _textMesh = new();
    // var arial = _app.LoadedFonts.First();
    var arial = fontFile;
    _lines = new List<Line>();
    var currentWord = new Word(_fontSize);
    var currentLine = new Line(arial.SpaceWidth, _fontSize, _maxLineSize);

    for (int i = 0; i < _text.Length; i++) {
      if (_text[i] == 32) {
        var isAdded = currentLine.TryAddWord(currentWord);
        if (!isAdded) {
          _lines.Add(currentLine);
          currentLine = new Line(arial.SpaceWidth, _fontSize, _maxLineSize);
          currentLine.TryAddWord(currentWord);
        }
        currentWord = new Word(_fontSize);
        continue;
      }

      var character = arial.GetCharacter(_text[i]);
      currentWord.AddCharacter(character);
    }
    CompleteStructure(arial, currentLine, currentWord);
    if (_textMesh.Indices.Length > 0) _hasIndexBuffer = true;
  }

  public void Draw(VkCommandBuffer commandBuffer) {
    if (_hasIndexBuffer) {
      vkCmdDrawIndexed(commandBuffer, (int)_indexCount, 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (int)_vertexCount, 1, 0, 0);
    }
  }

  private void CompleteStructure(FontFile fontFile, Line currentLine, Word currentWord) {
    var isAdded = currentLine.TryAddWord(currentWord);
    if (!isAdded) {
      _lines.Add(currentLine);
      currentLine = new Line(fontFile.SpaceWidth, _fontSize, _maxLineSize);
      currentLine.TryAddWord(currentWord);
    }
    _lines.Add(currentLine);
    CreateQuads();
    // CreateQuadsPrimitive();
    // MockQuads();
  }

  private void CreateQuads() {
    Logger.Info("Expected Values:");

    CreateQuadsPrimitive();

    Logger.Info("Incomming Values:");

    _textMesh = new();
    _numberOfLines = _lines.Count;
    var cursorX = 0f;
    var cursorY = 0f;

    for (int i = 0; i < _lines.Count; i++) {
      if (_isCentered) {
        cursorX = (_lines[i].MaxLength - _lines[i].LineLength) / 2;
      }
      for (int j = 0; j < _lines[i].Words.Count; j++) {
        foreach (var character in _lines[i].Words[j].Characters) {
          var tempMesh = new Mesh();
          AddCharacterVertices(
            ref tempMesh,
            new Vector2(cursorX, cursorY),
            character,
            new Vector2(character.XTextureCoord, -character.YTextureCoord),
            new Vector2(character.XMaxTextureCoord, -character.YMaxTextureCoord)
          );
          var tmpMaster = _textMesh.Vertices.ToList();
          tempMesh.Vertices[0].Position = new Vector3(-1.0f, -0.5f, 0.0f);
          tempMesh.Vertices[1].Position = new Vector3(-1.0f, 0.5f, 0.0f);
          tempMesh.Vertices[2].Position = new Vector3(-0f, 0.5f, 0.0f);
          tempMesh.Vertices[3].Position = new Vector3(-0f, 0.5f, 0.0f);
          tempMesh.Vertices[4].Position = new Vector3(-0f, -0.5f, 0.0f);
          tempMesh.Vertices[5].Position = new Vector3(-1f, -0.5f, 0.0f);
          tmpMaster.AddRange(tempMesh.Vertices);
          _textMesh.Vertices = tmpMaster.ToArray();

          Logger.Info("[VectorBlock Begin]");
          Logger.Info($"[VectorData] {tempMesh.Vertices[0].Position}");
          Logger.Info($"[VectorData] {tempMesh.Vertices[1].Position}");
          Logger.Info($"[VectorData] {tempMesh.Vertices[2].Position}");
          Logger.Info($"[VectorData] {tempMesh.Vertices[3].Position}");
          Logger.Info($"[VectorData] {tempMesh.Vertices[4].Position}");
          Logger.Info($"[VectorData] {tempMesh.Vertices[5].Position}");
          Logger.Info($"[UV] {tempMesh.Vertices[0].Uv}");
          Logger.Info($"[UV] {tempMesh.Vertices[1].Uv}");
          Logger.Info($"[UV] {tempMesh.Vertices[2].Uv}");
          Logger.Info($"[UV] {tempMesh.Vertices[3].Uv}");
          Logger.Info($"[UV] {tempMesh.Vertices[4].Uv}");
          Logger.Info($"[UV] {tempMesh.Vertices[5].Uv}");
          Logger.Info("[VectorBlock End]");
        }
      }
    }
  }

  private void CreateQuadsPrimitive() {
    _textMesh = new();
    AddVertices(
      ref _textMesh,
      new Vector2(-1.0f, -0.5f),
      new Vector2(-0.0f, 0.5f),
      new Vector2(0.0f, -0.0f),
      new Vector2(0.15234375f, -0.15625f)
    );

    var tmpMesh = new Mesh();
    AddVertices(
      ref tmpMesh,
      new Vector2(0.0f, -0.5f),
      new Vector2(1.0f, 0.5f),
      new Vector2(0.95f, -0.65f),
      new Vector2(0.85f, -0.75f)
    );

    var tmpVert = _textMesh.Vertices.ToList();
    tmpVert.AddRange(tmpMesh.Vertices);
    _textMesh.Vertices = tmpVert.ToArray();

    Logger.Info(_textMesh.Vertices[0].Position.ToString());
    Logger.Info(_textMesh.Vertices[1].Position.ToString());
    Logger.Info(_textMesh.Vertices[2].Position.ToString());
    Logger.Info(_textMesh.Vertices[3].Position.ToString());
    Logger.Info(_textMesh.Vertices[4].Position.ToString());
    Logger.Info(_textMesh.Vertices[5].Position.ToString());

    Logger.Info(_textMesh.Vertices[0].Uv.ToString());
    Logger.Info(_textMesh.Vertices[1].Uv.ToString());
    Logger.Info(_textMesh.Vertices[2].Uv.ToString());
    Logger.Info(_textMesh.Vertices[3].Uv.ToString());
    Logger.Info(_textMesh.Vertices[4].Uv.ToString());
    Logger.Info(_textMesh.Vertices[5].Uv.ToString());

  }

  private void MockQuads() {
    _textMesh = new();
    _textMesh.Vertices = new Vertex[6];

    var pos = new Vector2(-0.5f, -0.5f);
    var maxPos = new Vector2(0.5f, 0.5f);

    _textMesh.Vertices[0] = new Vertex {
      Position = new Vector3(pos.X, pos.Y, 0),
      Uv = new Vector2(-0.8496094f, -0.6542969f),
      Color = new Vector3(1, 1, 1)
    };

    _textMesh.Vertices[1] = new Vertex {
      Position = new Vector3(pos.X, maxPos.Y, 0),
      Uv = new Vector2(-0.8496094f, 0.09765625f),
      Color = new Vector3(1, 1, 1)
    };

    _textMesh.Vertices[2] = new Vertex {
      Position = new Vector3(maxPos.X, maxPos.Y, 0),
      Uv = new Vector2(0.080078125f, 0.09765625f),
      Color = new Vector3(1, 1, 1)
    };

    _textMesh.Vertices[3] = new Vertex {
      Position = new Vector3(maxPos.X, maxPos.Y, 0),
      Uv = new Vector2(0.080078125f, 0.09765625f),
      Color = new Vector3(1, 1, 1)
    };

    _textMesh.Vertices[4] = new Vertex {
      Position = new Vector3(maxPos.X, pos.Y, 0),
      Uv = new Vector2(0.080078125f, 0.6542969f),
      Color = new Vector3(1, 1, 1)
    };

    _textMesh.Vertices[5] = new Vertex {
      Position = new Vector3(pos.X, pos.Y, 0),
      Uv = new Vector2(0.8496094f, 0.6542969f),
      Color = new Vector3(1, 1, 1)
    };
  }

  private void UpdatePosition() {
    CheckBuffers(_textMesh.Vertices);
  }

  private void UpdatePosition_BC() {
    _textMesh = new();
    var arial = _app.LoadedFonts[0];
    var pos = Owner!.GetComponent<Transform>().Position;
    var scale = Owner!.GetComponent<Transform>().Scale;

    for (int i = 0; i < _text.Length; i++) {
      if (_text[i] == 32) continue;
      var targetCharacterData = arial.GetCharacter(_text[i]);
      float xPos = pos.X + ((float)targetCharacterData.XOffset * scale.X);
      float yPos = pos.Y - (float)(targetCharacterData.SizeY - targetCharacterData.YOffset) * scale.Y;
      float w = (float)targetCharacterData.SizeX * scale.X;
      float h = (float)targetCharacterData.SizeY * scale.Y;

      var tempVert = new Mesh();
      //AddVertices(ref tempVert, xPos, yPos)

      /*
      tempVert.Vertices = new Vertex[6];
      tempVert.Vertices[0] = new Vertex {
        // Position = new Vector3(xPos, yPos + h, 0),
        Position = new Vector3(pos.X - 0.5f, pos.Y + 0.5f, pos.Z),
        Uv = new Vector2(1.0f, 0.0f),
        Color = new Vector3(1, 1, 1),
        // Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[1] = new Vertex {
        // Position = new Vector3(xPos, yPos, 0),
        Position = new Vector3(pos.X + 0.5f, pos.Y + 0.5f, pos.Z),
        Uv = new Vector2(0.0f, 0.0f),
        Color = new Vector3(1, 1, 1),
        // Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[2] = new Vertex {
        // Position = new Vector3(xPos + w, yPos, 0),
        Position = new Vector3(pos.X + 0.5f, pos.Y - 0.5f, pos.Z),
        Uv = new Vector2(0.0f, 1.0f),
        Color = new Vector3(1, 1, 1),
        // Normal = new Vector3(1, 1, 1)
      };

      tempVert.Vertices[3] = new Vertex {
        // Position = new Vector3(xPos, yPos + h, 0),
        Position = new Vector3(pos.X + 0.5f, pos.Y - 0.5f, pos.Z),
        Uv = new Vector2(0.0f, 1.0f),
        Color = new Vector3(1, 1, 1),
        // Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[4] = new Vertex {
        // Position = new Vector3(xPos + w, yPos, 0),
        Position = new Vector3(pos.X - 0.5f, pos.Y - 0.5f, pos.Z),
        Uv = new Vector2(1.0f, 1.0f),
        Color = new Vector3(1, 1, 1),
        // Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[5] = new Vertex {
        // Position = new Vector3(xPos + w, yPos + h, 0),
        Position = new Vector3(pos.X - 0.5f, pos.Y + 0.5f, pos.Z),
        Uv = new Vector2(1.0f, 0.0f),
        Color = new Vector3(1, 1, 1),
        // Normal = new Vector3(1, 1, 1)
      };
      */

      var current = _textMesh.Vertices.ToList();
      current.AddRange(tempVert.Vertices);
      _textMesh.Vertices = current.ToArray();

      //var shift = 
      //pos.X += (float)(targetCharacterData.XAdvance >> 6) * scale.X;
    }
    /*
    _textMesh.Vertices = new Vertex[4];
    _textMesh.Vertices[0] = new Vertex {
      Position = new Vector3(0.5f, 0.5f, 0.0f),
      Uv = new Vector2(0.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _textMesh.Vertices[1] = new Vertex {
      Position = new Vector3(0.5f, -0.5f, 0.0f),
      Uv = new Vector2(0.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _textMesh.Vertices[2] = new Vertex {
      Position = new Vector3(-0.5f, -0.5f, 0.0f),
      Uv = new Vector2(1.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _textMesh.Vertices[3] = new Vertex {
      Position = new Vector3(-0.5f, 0.5f, 0.0f),
      Uv = new Vector2(1.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    */

    CheckBuffers(_textMesh.Vertices);
  }

  private void CheckBuffers(Vertex[] vertices) {
    if (_vertexCount == (ulong)vertices.Length) return;

    vkDeviceWaitIdle(_device.LogicalDevice);
    Dispose();
    CreateVertexBuffer(vertices);
  }

  private void AddCharacterVertices(ref Mesh mesh, Vector2 cursor, Character character, Vector2 uvPos, Vector2 uvMaxPos) {
    var x = cursor.X + (character.XOffset * _fontSize);
    var y = cursor.Y + (character.YOffset * _fontSize);
    var maxX = x + (character.XOffset + _fontSize);
    var maxY = y + (character.YOffset + _fontSize);
    var properX = (2 * x) - 1;
    var properY = (-2 * y) + 1;
    var properMaxX = (2 * maxX) - 1;
    var properMaxY = (-2 * maxY) + 1;

    AddVertices(
      ref mesh,
      new Vector2(properX, properY),
      new Vector2(properMaxX, properMaxY),
      uvPos,
      uvMaxPos
    );
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

    var stagingBuffer = new Dwarf.Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(Utils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new Dwarf.Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount,
      VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(uint[] indices) {
    _indexCount = (ulong)indices.Length;
    if (!_hasIndexBuffer) return;

    ulong bufferSize = (ulong)sizeof(uint) * _indexCount;
    ulong indexSize = (ulong)sizeof(uint);

    var stagingBuffer = new Dwarf.Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(Utils.ToIntPtr(indices), bufferSize);
    stagingBuffer.Unmap();

    _indexBuffer = new Dwarf.Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
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

  public unsafe void Bind(VkCommandBuffer commandBuffer) {
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffer.GetBuffer() };
    ulong[] offsets = { 0 };
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }
  }

  public void Dispose() {
    _vertexBuffer?.Dispose();
    if (_hasIndexBuffer) {
      _indexBuffer?.Dispose();
    }
  }

  public void SetText(string text) {
    _text = text;
  }

  public Guid GetTextureIdReference() {
    return _textAtlasId;
  }
}
