using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using System.Numerics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;
using StbImageSharp;
using Dwarf.AbstractionLayer;

namespace Dwarf.Engine.Rendering.UI;
public class GuiTexture : Component, IUIElement {
  private readonly VulkanDevice _device = null!;

  private Mesh _mesh = null!;
  private bool _hasIndexBuffer = false;
  private Dwarf.Vulkan.Buffer _vertexBuffer = null!;
  private Dwarf.Vulkan.Buffer _indexBuffer = null!;
  private ulong _vertexCount = 0;
  private ulong _indexCount = 0;
  private Guid _textureIdRef = Guid.Empty;
  private bool _usesTexture = false;

  private Vector2 _baseTextureSize = Vector2.Zero;

  public GuiTexture() { }

  public GuiTexture(VulkanDevice device) {
    _device = device;

    CreateVertexData();

    if (_mesh.Indices.Length > 0) _hasIndexBuffer = true;
    CreateVertexBuffer(_mesh.Vertices);
    CreateIndexBuffer(_mesh.Indices);
  }

  public void Bind(VkCommandBuffer commandBuffer) {
    throw new NotImplementedException();
  }

  public unsafe Task Bind(VkCommandBuffer commandBuffer, uint index = 0) {
    VkBuffer[] buffers = new VkBuffer[] { _vertexBuffer.GetBuffer() };
    ulong[] offsets = { 0 };
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }

    if (_hasIndexBuffer) {
      vkCmdBindIndexBuffer(commandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint32);
    }
    return Task.CompletedTask;
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

  public void Dispose() {
    _vertexBuffer?.Dispose();
    if (_hasIndexBuffer) {
      _indexBuffer?.Dispose();
    }
  }

  public Task Draw(VkCommandBuffer commandBuffer, uint index = 0) {
    if (_hasIndexBuffer) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount, 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount, 1, 0, 0);
    }
    return Task.CompletedTask;
  }

  public void DrawText(string text) {
    // throw new NotImplementedException();
    CreateVertexData();
  }

  public Guid GetTextureIdReference() {
    return _textureIdRef;
  }

  public void Update() {
    // throw new NotImplementedException();
  }

  public void BindToTexture(TextureManager textureManager, string texturePath, bool useLocalPath = false) {
    if (useLocalPath) {
      _textureIdRef = textureManager.GetTextureId($"./Textures/{texturePath}");
    } else {
      _textureIdRef = textureManager.GetTextureId(texturePath);
    }

    if (_textureIdRef != Guid.Empty) {
      _usesTexture = true;
      if (useLocalPath) {
        GetTextureSize($"./Textures/{texturePath}");
      } else {
        GetTextureSize(texturePath);
      }

    } else {
      Logger.Warn($"Could not bind texture to GuiTexture ({texturePath}) - no such texture in manager");
    }
  }

  private void GetTextureSize(string texturePath) {
    using var stream = File.OpenRead(texturePath);
    var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

    _baseTextureSize = new(image.Width, image.Height);

    stream.Dispose();
  }

  private void RecreateBuffers() {

  }

  private void CreateVertexData() {
    _mesh = new();

    _mesh.Vertices = new Vertex[4];
    _mesh.Vertices[0] = new Vertex {
      Position = new Vector3(0.5f, 0.5f, 0.0f),
      Uv = new Vector2(0.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _mesh.Vertices[1] = new Vertex {
      Position = new Vector3(0.5f, -0.5f, 0.0f),
      Uv = new Vector2(0.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _mesh.Vertices[2] = new Vertex {
      Position = new Vector3(-0.5f, -0.5f, 0.0f),
      Uv = new Vector2(1.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _mesh.Vertices[3] = new Vertex {
      Position = new Vector3(-0.5f, 0.5f, 0.0f),
      Uv = new Vector2(1.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };

    _mesh.Indices = new uint[] {
      0, 1, 3, // first triangle
      1, 2, 3  // second triangle
    };
  }

  private unsafe void CreateVertexBuffer(Vertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;

    ulong bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * _vertexCount;
    ulong vertexSize = ((ulong)Unsafe.SizeOf<Vertex>());

    var stagingBuffer = new Dwarf.Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new Vulkan.Buffer(
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

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(indices), bufferSize);
    //stagingBuffer.Unmap();

    _indexBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  public Mesh Mesh => _mesh;
}
