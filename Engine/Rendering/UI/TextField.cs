using System.Runtime.CompilerServices;

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
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
  private string _text = string.Empty;
  private Mesh _textMesh = null!;
  private Guid _textAtlasId = Guid.Empty;

  private ulong _vertexCount = 0;

  public TextField() { }

  public TextField(Application app, string text) {
    _app = app;
    _text = text;
    _device = _app.Device;
  }

  public void RenderText() {
    UpdatePosition();
  }

  public void Draw(VkCommandBuffer commandBuffer) {
    vkCmdDraw(commandBuffer, (int)_vertexCount, 1, 0, 0);
  }

  private void UpdatePosition() {
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
      tempVert.Vertices = new Vertex[6];

      tempVert.Vertices[0] = new Vertex {
        Position = new Vector3(xPos, yPos + h, 0),
        Uv = new Vector2(0.0f, 0.0f),
        Color = new Vector3(1, 1, 1),
        Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[1] = new Vertex {
        Position = new Vector3(xPos, yPos, 0),
        Uv = new Vector2(0.0f, 1.0f),
        Color = new Vector3(1, 1, 1),
        Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[2] = new Vertex {
        Position = new Vector3(xPos + w, yPos, 0),
        Uv = new Vector2(1.0f, 1.0f),
        Color = new Vector3(1, 1, 1),
        Normal = new Vector3(1, 1, 1)
      };

      tempVert.Vertices[3] = new Vertex {
        Position = new Vector3(xPos, yPos + h, 0),
        Uv = new Vector2(0.0f, 0.0f),
        Color = new Vector3(1, 1, 1),
        Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[4] = new Vertex {
        Position = new Vector3(xPos + w, yPos, 0),
        Uv = new Vector2(1.0f, 1.0f),
        Color = new Vector3(1, 1, 1),
        Normal = new Vector3(1, 1, 1)
      };
      tempVert.Vertices[5] = new Vertex {
        Position = new Vector3(xPos + w, yPos + h, 0),
        Uv = new Vector2(1.0f, 0.0f),
        Color = new Vector3(1, 1, 1),
        Normal = new Vector3(1, 1, 1)
      };

      var current = _textMesh.Vertices.ToList();
      current.AddRange(tempVert.Vertices);
      _textMesh.Vertices = current.ToArray();

      //var shift = 
      //pos.X += (float)(targetCharacterData.XAdvance >> 6) * scale.X;
    }

    CheckBuffers(_textMesh.Vertices);
  }

  private void CheckBuffers(Vertex[] vertices) {
    if (_vertexCount == (ulong)vertices.Length) return;

    vkDeviceWaitIdle(_device.LogicalDevice);
    Dispose();
    CreateVertexBuffer(vertices);
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
  }

  public void SetText(string text) {
    _text = text;
  }

  public Guid GetTextureIdReference() {
    return _textAtlasId;
  }
}
