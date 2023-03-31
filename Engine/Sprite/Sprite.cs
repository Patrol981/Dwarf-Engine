using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Vulkan;

using static Vortice.Vulkan.Vulkan;
using Vortice.Vulkan;
using Dwarf.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dwarf.Engine;
public class Sprite : Component, IDisposable {
  private readonly Device _device = null!;

  private Dwarf.Vulkan.Buffer _vertexBuffer = null!;
  private Dwarf.Vulkan.Buffer _indexBuffer = null!;
  private Guid _textureIdRef = Guid.Empty;
  private bool _hasIndexBuffer = false;
  private bool _usesTexture = false;

  private ulong _vertexCount = 0;
  private ulong _indexCount = 0;

  private Mesh _spriteMesh = null!;

  private float[] _vertices = {
    0.5f,  0.5f, 0.0f,  1.0f, 1.0f, // top right
    0.5f, -0.5f, 0.0f,  1.0f, 0.0f, // bottom right
    -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, // bottom left
    -0.5f,  0.5f, 0.0f,  0.0f, 1.0f  // top left 
  };

  private int[] _indices = {
    0, 1, 3, // first triangle
    1, 2, 3  // second triangle
  };

  public Sprite() { }

  public Sprite(Device device) {
    _device = device;

    CreateSpriteVertexData();

    if (_spriteMesh.Indices.Length > 0) _hasIndexBuffer = true;
    CreateVertexBuffer(_spriteMesh.Vertices);
    CreateIndexBuffer(_spriteMesh.Indices);
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

    if (_hasIndexBuffer) {
      vkCmdBindIndexBuffer(commandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint32);
    }
  }

  public void BindToTexture(
    TextureManager textureManager,
    string texturePath,
    bool useLocalPath = false,
    int modelPart = 0
  ) {
    if (useLocalPath) {
      _textureIdRef = textureManager.GetTextureId($"./Textures/{texturePath}");
    } else {
      _textureIdRef = textureManager.GetTextureId(texturePath);
    }

    if (_textureIdRef != Guid.Empty) {
      _usesTexture = true;
    } else {
      Logger.Warn($"Could not bind texture to model ({texturePath}) - no such texture in manager");
    }
  }

  public void Draw(VkCommandBuffer commandBuffer) {
    if (_hasIndexBuffer) {
      vkCmdDrawIndexed(commandBuffer, (int)_indexCount, 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (int)_vertexCount, 1, 0, 0);
    }
  }

  private void CreateSpriteVertexData() {
    _spriteMesh = new();

    _spriteMesh.Vertices = new Vertex[4];
    _spriteMesh.Vertices[0] = new Vertex {
      Position = new Vector3(0.5f, 0.5f, 0.0f),
      Uv = new Vector2(0.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[1] = new Vertex {
      Position = new Vector3(0.5f, -0.5f, 0.0f),
      Uv = new Vector2(0.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[2] = new Vertex {
      Position = new Vector3(-0.5f, -0.5f, 0.0f),
      Uv = new Vector2(1.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[3] = new Vertex {
      Position = new Vector3(-0.5f, 0.5f, 0.0f),
      Uv = new Vector2(1.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };

    _spriteMesh.Indices = new uint[] {
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
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(Utils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new Vulkan.Buffer(
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

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(Utils.ToIntPtr(indices), bufferSize);
    //stagingBuffer.Unmap();

    _indexBuffer = new Vulkan.Buffer(
      _device,
      indexSize,
      _indexCount,
      VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  public void Dispose() {
    _vertexBuffer?.Dispose();
    if (_hasIndexBuffer) {
      _indexBuffer?.Dispose();
    }
  }
  public bool UsesTexture => _usesTexture;
  public Guid GetTextureIdReference() {
    return _textureIdRef;
  }
}
