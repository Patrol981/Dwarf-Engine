using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Renderer2D;
public class Sprite : Component, IDrawable2D, I2DCollision {
  private readonly VulkanDevice _device;
  private readonly VmaAllocator _vmaAllocator;
  private readonly TextureManager _textureManager;
  private readonly IRenderer _renderer;

  private Guid _textureIdRef = Guid.Empty;
  private Mesh _spriteMesh = null!;
  private VulkanTexture _spriteTexture = null!;
  private Vector3 _lastKnownScale = Vector3.Zero;
  private Vector2 _cachedSize = Vector2.Zero;
  private Bounds2D _cachedBounds = Bounds2D.Zero;

  public Sprite() {
    _device = null!;
    _vmaAllocator = VmaAllocator.Null;
    _textureManager = null!;
    _renderer = null!;
  }

  public Sprite(Application app, string? path) {
    _device = app.Device;
    _vmaAllocator = app.VmaAllocator;
    _textureManager = app.TextureManager;
    _renderer = app.Renderer;

    CreateSpriteVertexData();

    if (!string.IsNullOrEmpty(path)) {
      _spriteTexture = (VulkanTexture)_textureManager.AddTextureLocal(path).Result;
      _textureIdRef = _textureManager.GetTextureIdLocal(_spriteTexture.TextureName);
      UsesTexture = true;
    }

    _spriteMesh.CreateVertexBuffer();
    _spriteMesh.CreateIndexBuffer();
  }

  public void BuildDescriptors(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    _spriteTexture.BuildDescriptor(descriptorSetLayout, descriptorPool);
  }

  public Task Bind(nint commandBuffer, uint index) {
    _renderer.CommandList.BindVertex(commandBuffer, _spriteMesh.VertexBuffer!, index);
    if (_spriteMesh.HasIndexBuffer) _renderer.CommandList.BindIndex(commandBuffer, _spriteMesh.IndexBuffer!, index);

    return Task.CompletedTask;
  }

  public Task Draw(nint commandBuffer, uint index = 0, uint firstInstance = 0) {
    if (_spriteMesh.HasIndexBuffer) {
      _renderer.CommandList.DrawIndexed(commandBuffer, _spriteMesh.IndexCount, 1, index, 0, firstInstance);
    } else {
      _renderer.CommandList.Draw(commandBuffer, _spriteMesh.VertexCount, 1, 0, 0);
    }

    return Task.CompletedTask;
  }

  private void CreateSpriteVertexData() {
    _spriteMesh = new(_vmaAllocator, _device) {
      Vertices = new Vertex[4]
    };
    _spriteMesh.Vertices[0] = new Vertex {
      Position = new Vector3(0.25f, 0.25f, 0.0f),
      Uv = new Vector2(0.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[1] = new Vertex {
      Position = new Vector3(0.25f, -0.25f, 0.0f),
      Uv = new Vector2(0.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[2] = new Vertex {
      Position = new Vector3(-0.25f, -0.25f, 0.0f),
      Uv = new Vector2(-1.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[3] = new Vertex {
      Position = new Vector3(-0.25f, 0.25f, 0.0f),
      Uv = new Vector2(-1.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };

    // _spriteMesh.Indices = [
    //   0, 1, 3, // first triangle
    //   1, 2, 3  // second triangle
    // ];

    _spriteMesh.Indices = [
      3, 1, 0, // first triangle
      3, 2, 1  // second triangle
    ];
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
    _spriteMesh = new(_vmaAllocator, _device);

    for (uint y = 0; y < image.Height; y++) {
      for (uint x = 0; x < image.Width; x++) {

      }
    }
  }

  private void CreateStandardVertices(ref ImageResult image) {
    var size = new Vector2(image.Width, image.Height);
    var aspect = MathF.Round(image.Width / image.Height);
    if (aspect < 1) aspect = MathF.Round(image.Height / image.Width);

    if (aspect != 1) {
      AddPositionsToVertices(size, aspect);
    }
  }

  // private void SetupProportions(string texturePath, bool pixelPerfect = false) {
  //   using var stream = File.OpenRead(texturePath);
  //   var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

  //   if (pixelPerfect) {
  //     CreatePixelPerfectVertices(ref image);
  //   } else {
  //     CreateStandardVertices(ref image);
  //   }

  //   // _device.WaitDevice();
  //   Dispose();

  //   CreateVertexBuffer(_spriteMesh.Vertices);
  //   CreateIndexBuffer(_spriteMesh.Indices);

  //   stream.Dispose();
  // }

  private Bounds2D GetBounds() {
    var pos = Owner!.GetComponent<Transform>().Position;
    var size = GetSize();

    _cachedBounds = new() {
      Min = new Vector2(pos.X, pos.Y),
      Max = new Vector2(pos.X + size.X, pos.Y + size.Y)
    };

    return _cachedBounds;
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

    return _cachedSize;
  }

  public void Dispose() {
    _spriteMesh.Dispose();
    GC.SuppressFinalize(this);
  }
  public bool UsesTexture { get; private set; } = false;
  public Guid GetTextureIdReference() {
    return _textureIdRef;
  }
  public Vector2 Size => GetSize();
  public Bounds2D Bounds => GetBounds();

  public bool IsUI => false;

  public Entity Entity => Owner;
  public bool Active => Owner.Active;
  public ITexture Texture => _spriteTexture;
}
