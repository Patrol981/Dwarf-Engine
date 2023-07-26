﻿using System;
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
using StbImageSharp;
using System.Drawing;

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
  private Vector3 _lastKnownScale = Vector3.Zero;
  private Vector2 _cachedSize = Vector2.Zero;

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
      if (useLocalPath) {
        SetupProportions($"./Textures/{texturePath}");
      } else {
        SetupProportions(texturePath);
      }

    } else {
      Logger.Warn($"Could not bind texture to sprite ({texturePath}) - no such texture in manager");
    }
  }

  public void Draw(VkCommandBuffer commandBuffer) {
    if (_hasIndexBuffer) {
      vkCmdDrawIndexed(commandBuffer, (uint)_indexCount, 1, 0, 0, 0);
    } else {
      vkCmdDraw(commandBuffer, (uint)_vertexCount, 1, 0, 0);
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

  private void AddPositionsToVertices(Vector2 size, float aspect) {
    var len = MathF.Round(aspect);
    var side = false;

    if (size.X > size.Y) {
      for (uint i = 0; i < len; i++) {
        if (i == (uint)len - 1 && i + 1 % 2 != 0) {
          var leftAdd = new Vector3(0.25f, 0, 0);
          var rightAdd = new Vector3(-0.25f, 0, 0);
          _spriteMesh.Vertices[0].Position = Vector3.Add(_spriteMesh.Vertices[0].Position, leftAdd);
          _spriteMesh.Vertices[1].Position = Vector3.Add(_spriteMesh.Vertices[1].Position, leftAdd);

          _spriteMesh.Vertices[2].Position = Vector3.Add(_spriteMesh.Vertices[2].Position, rightAdd);
          _spriteMesh.Vertices[3].Position = Vector3.Add(_spriteMesh.Vertices[3].Position, rightAdd);

          break;
        }
        if (!side) {
          var newVec = Vector3.Add(_spriteMesh.Vertices[0].Position, new Vector3(0.75f, 0, 0));
          _spriteMesh.Vertices[0].Position = newVec;
          newVec = Vector3.Add(_spriteMesh.Vertices[1].Position, new Vector3(0.75f, 0, 0));
          _spriteMesh.Vertices[1].Position = newVec;
          side = true;
        } else {
          var newVec = Vector3.Add(_spriteMesh.Vertices[2].Position, new Vector3(-0.75f, 0, 0));
          _spriteMesh.Vertices[2].Position = newVec;
          newVec = Vector3.Add(_spriteMesh.Vertices[3].Position, new Vector3(-0.75f, 0, 0));
          _spriteMesh.Vertices[3].Position = newVec;
          side = false;
        }
      }
    } else {
      for (uint i = 0; i < len; i++) {
        if (i == (uint)len - 1 && i + 1 % 2 != 0) {
          var bottomAdd = new Vector3(0f, 0.25f, 0);
          var topAdd = new Vector3(0f, -0.25f, 0);

          _spriteMesh.Vertices[0].Position = Vector3.Add(_spriteMesh.Vertices[0].Position, bottomAdd);
          _spriteMesh.Vertices[1].Position = Vector3.Add(_spriteMesh.Vertices[1].Position, topAdd);

          _spriteMesh.Vertices[3].Position = Vector3.Add(_spriteMesh.Vertices[3].Position, bottomAdd);
          _spriteMesh.Vertices[2].Position = Vector3.Add(_spriteMesh.Vertices[2].Position, topAdd);

          break;
        }
        if (!side) {
          var newVec = Vector3.Add(_spriteMesh.Vertices[0].Position, new Vector3(0, 0.75f, 0));
          _spriteMesh.Vertices[0].Position = newVec;
          newVec = Vector3.Add(_spriteMesh.Vertices[3].Position, new Vector3(0, 0.75f, 0));
          _spriteMesh.Vertices[3].Position = newVec;
          side = true;
        } else {
          var newVec = Vector3.Add(_spriteMesh.Vertices[1].Position, new Vector3(0, -0.75f, 0));
          _spriteMesh.Vertices[1].Position = newVec;
          newVec = Vector3.Add(_spriteMesh.Vertices[2].Position, new Vector3(0, -0.75f, 0));
          _spriteMesh.Vertices[2].Position = newVec;
          side = false;
        }
      }
    }
  }

  private void CreatePixelPerfectVertices(ref ImageResult image) {
    _spriteMesh = new();

    for (uint y = 0; y < image.Height; y++) {
      for (uint x = 0; x < image.Width; x++) {

      }
    }
  }

  private void CreateStandardVertices(ref ImageResult image) {
    var size = new Vector2(image.Width, image.Height);
    var aspect = MathF.Round(image.Width / image.Height);
    if (aspect < 1) aspect = MathF.Round(image.Height / image.Width);

    Logger.Info($"Aspect: {aspect} | {image.Width}x{image.Height}");

    if (aspect != 1) {
      AddPositionsToVertices(size, aspect);
    }
  }

  private void SetupProportions(string texturePath, bool pixelPerfect = false) {
    using var stream = File.OpenRead(texturePath);
    var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

    if (pixelPerfect) {
      CreatePixelPerfectVertices(ref image);
    } else {
      CreateStandardVertices(ref image);
    }

    vkDeviceWaitIdle(_device.LogicalDevice);
    Dispose();

    CreateVertexBuffer(_spriteMesh.Vertices);
    CreateIndexBuffer(_spriteMesh.Indices);

    stream.Dispose();
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

  private Vector2 GetSize() {
    var scale = Owner!.GetComponent<Transform>().Scale;
    if (_lastKnownScale == scale) return _cachedSize;

    float minX, minY, maxX, maxY;

    maxX = _spriteMesh.Vertices[0].Position.X;
    maxY = _spriteMesh.Vertices[0].Position.Y;
    minX = _spriteMesh.Vertices[0].Position.X;
    minY = _spriteMesh.Vertices[0].Position.Y;

    for (int i = 0; i < _spriteMesh.Vertices.Length; i++) {
      if (minX > _spriteMesh.Vertices[i].Position.X) minX = _spriteMesh.Vertices[i].Position.X;
      if (maxX < _spriteMesh.Vertices[i].Position.X) maxX = _spriteMesh.Vertices[i].Position.X;

      if (minY > _spriteMesh.Vertices[i].Position.Y) minY = _spriteMesh.Vertices[i].Position.Y;
      if (maxY < _spriteMesh.Vertices[i].Position.Y) maxY = _spriteMesh.Vertices[i].Position.Y;
    }

    _lastKnownScale = scale;


    _cachedSize = new Vector2(
      MathF.Abs(minX - maxX) * scale.X,
      MathF.Abs(minY - maxY) * scale.Y
    );

    /*
    _cachedSize = new Vector2(
      (MathF.Abs(minX) + MathF.Abs(maxX)) * scale.X,
      (MathF.Abs(minY) + MathF.Abs(maxY)) * scale.Y
    );
    */

    return _cachedSize;
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
  public Vector2 Size => GetSize();
}