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
  private const float ASPECT_ONE = 1.0f;
  private const float VERTEX_SIZE = 0.2f;

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
  private float _aspectRatio = ASPECT_ONE;

  public Sprite() {
    _device = null!;
    _vmaAllocator = VmaAllocator.Null;
    _textureManager = null!;
    _renderer = null!;
  }

  public Sprite(Application app, string? path, bool isSpriteSheet = false, int flip = 1) {
    _device = app.Device;
    _vmaAllocator = app.VmaAllocator;
    _textureManager = app.TextureManager;
    _renderer = app.Renderer;

    if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path), path);

    _spriteTexture = (VulkanTexture)_textureManager.AddTextureLocal(path, flip).Result;
    _textureIdRef = _textureManager.GetTextureIdLocal(_spriteTexture.TextureName);
    UsesTexture = true;

    GetAspectRatio();
    if (_aspectRatio == ASPECT_ONE) {
      CreateSpriteVertexBox();
    } else {
      CreateSpriteVertexWithAspect();
    }

    if (isSpriteSheet) {
      HandleSpriteSheetUVs();
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

  private void GetAspectRatio() {
    if (_spriteTexture.Width > _spriteTexture.Height) {
      _aspectRatio = (float)_spriteTexture.Height / _spriteTexture.Width;
    } else {
      _aspectRatio = (float)_spriteTexture.Width / _spriteTexture.Height;
    }
  }

  private void CreateSpriteVertexWithAspect() {
    CreateSpriteVertexBox();

    if (_spriteTexture.Width > _spriteTexture.Height) {
      _spriteMesh.Vertices[0].Position.X += _aspectRatio;
      _spriteMesh.Vertices[1].Position.X += _aspectRatio;
      _spriteMesh.Vertices[2].Position.X -= _aspectRatio;
      _spriteMesh.Vertices[3].Position.X -= _aspectRatio;
    } else {
      _spriteMesh.Vertices[0].Position.Y += _aspectRatio;
      _spriteMesh.Vertices[1].Position.Y -= _aspectRatio;
      _spriteMesh.Vertices[2].Position.Y -= _aspectRatio;
      _spriteMesh.Vertices[3].Position.Y += _aspectRatio;
    }
  }

  private void CreateSpriteVertexBox() {
    _spriteMesh = new(_vmaAllocator, _device) {
      Vertices = new Vertex[4]
    };
    _spriteMesh.Vertices[0] = new Vertex {
      Position = new Vector3(VERTEX_SIZE, VERTEX_SIZE, 0.0f),
      Uv = new Vector2(0.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[1] = new Vertex {
      Position = new Vector3(VERTEX_SIZE, -VERTEX_SIZE, 0.0f),
      Uv = new Vector2(0.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[2] = new Vertex {
      Position = new Vector3(-VERTEX_SIZE, -VERTEX_SIZE, 0.0f),
      Uv = new Vector2(-1.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(1, 1, 1)
    };
    _spriteMesh.Vertices[3] = new Vertex {
      Position = new Vector3(-VERTEX_SIZE, VERTEX_SIZE, 0.0f),
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

  private void HandleSpriteSheetUVs() {
    var (uMin, uMax, vMin, vMax) = GetUVCoords(0, 1);

    // // _spriteMesh.Vertices[0].Uv.X = uMin;
    // _spriteMesh.Vertices[0].Uv.Y = 1.0f - vMin;

    // // _spriteMesh.Vertices[1].Uv.X = uMax;
    // _spriteMesh.Vertices[1].Uv.Y = 1.0f;

    // // _spriteMesh.Vertices[2].Uv.X = uMax;
    // _spriteMesh.Vertices[2].Uv.Y = 1.0f;

    // // _spriteMesh.Vertices[3].Uv.X = uMin;
    // _spriteMesh.Vertices[3].Uv.Y = 1.0f - vMax;

    Logger.Info($"{uMin} {uMax} {vMin} {vMax}");

    // _spriteMesh.Vertices[0].Position.X /= 2;
    _spriteMesh.Vertices[0].Uv.X = 0.0f;
    _spriteMesh.Vertices[0].Uv.Y = 1.0f - 0.1666f;

    // _spriteMesh.Vertices[1].Position.X /= 2;
    _spriteMesh.Vertices[1].Uv.X = 0.0f;
    _spriteMesh.Vertices[1].Uv.Y = 1.0f;

    // _spriteMesh.Vertices[2].Position.X /= 2;
    _spriteMesh.Vertices[2].Uv.X = -1.0f;
    _spriteMesh.Vertices[2].Uv.Y = 1.0f;

    // _spriteMesh.Vertices[3].Position.X /= 2;
    _spriteMesh.Vertices[3].Uv.X = -1.0f;
    _spriteMesh.Vertices[3].Uv.Y = 1.0f - 0.1666f;
  }

  private (float, float, float, float) GetUVCoords(int x, int y) {
    int col = y;
    int row = x;

    float uvSize = 1.0f / _spriteTexture.Height;
    float uMin = col * uvSize;
    float vMin = 1.0f - (row + 1) * uvSize;
    float uMax = (col + 1) * uvSize;
    float vMax = 1.0f - row * uvSize;

    (vMax, vMin) = (vMin, vMax);

    return (uMin, uMax, vMin, vMax);
  }

  [Obsolete]
  private void AddPositionsToVertices(Vector2 size, float aspect) {
    var len = MathF.Round(aspect);
    var side = false;

    if (size.X > size.Y) {
      for (uint i = 0; i < len; i++) {
        if (i == (uint)len - 1 && i + 1 % 2 != 0) {
          var leftAdd = new Vector3(0.5f, 0, 0);
          var rightAdd = new Vector3(-0.5f, 0, 0);
          _spriteMesh.Vertices[0].Position = Vector3.Add(_spriteMesh.Vertices[0].Position, leftAdd);
          _spriteMesh.Vertices[1].Position = Vector3.Add(_spriteMesh.Vertices[1].Position, leftAdd);

          _spriteMesh.Vertices[2].Position = Vector3.Add(_spriteMesh.Vertices[2].Position, rightAdd);
          _spriteMesh.Vertices[3].Position = Vector3.Add(_spriteMesh.Vertices[3].Position, rightAdd);

          break;
        }
        if (!side) {
          var newVec = Vector3.Add(_spriteMesh.Vertices[0].Position, new Vector3(0.15f, 0, 0));
          _spriteMesh.Vertices[0].Position = newVec;
          newVec = Vector3.Add(_spriteMesh.Vertices[1].Position, new Vector3(0.15f, 0, 0));
          _spriteMesh.Vertices[1].Position = newVec;
          side = true;
        } else {
          var newVec = Vector3.Add(_spriteMesh.Vertices[2].Position, new Vector3(-0.15f, 0, 0));
          _spriteMesh.Vertices[2].Position = newVec;
          newVec = Vector3.Add(_spriteMesh.Vertices[3].Position, new Vector3(-0.15f, 0, 0));
          _spriteMesh.Vertices[3].Position = newVec;
          side = false;
        }
      }
    } else {
      for (uint i = 0; i < len; i++) {
        if (i == (uint)len - 1 && i + 1 % 2 != 0) {
          var bottomAdd = new Vector3(0f, 0.5f, 0);
          var topAdd = new Vector3(0f, -0.5f, 0);

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

  [Obsolete]
  private void CreatePixelPerfectVertices(ref ImageResult image) {
    _spriteMesh = new(_vmaAllocator, _device);

    for (uint y = 0; y < image.Height; y++) {
      for (uint x = 0; x < image.Width; x++) {

      }
    }
  }

  [Obsolete]
  private void CreateStandardVertices(ref ImageResult image) {
    var size = new Vector2(image.Width, image.Height);
    var aspect = MathF.Round(image.Width / image.Height);
    if (aspect < 1) aspect = MathF.Round(image.Height / image.Width);

    if (aspect != 1) {
      AddPositionsToVertices(size, aspect);
    }
  }

  [Obsolete]
  private void SetupProportions(string texturePath, bool pixelPerfect = false) {
    using var stream = File.OpenRead(texturePath);
    var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

    if (pixelPerfect) {
      CreatePixelPerfectVertices(ref image);
    } else {
      CreateStandardVertices(ref image);
    }

    // _device.WaitDevice();
    Dispose();
    _spriteMesh.CreateIndexBuffer();
    _spriteMesh.CreateVertexBuffer();

    stream.Dispose();
  }

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
