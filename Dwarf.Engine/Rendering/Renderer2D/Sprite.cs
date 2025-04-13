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
  public const float SPRITE_TILE_SIZE_NONE = -1.0f;
  public const float SPRITE_TILE_SIZE_AUTO = 0.0f;
  public const int SPRITE_COUNT_NONE = -1;
  public const int SPRITE_COUNT_AUTO = 0;

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
  private float _spriteSheetTileSize = SPRITE_TILE_SIZE_NONE;
  private int _spritesPerRow = 1;
  private int _spritesPerColumn = 1;
  private bool _isSpriteSheet;

  private Vector2 _stride = Vector2.Zero;

  public Sprite() {
    _device = null!;
    _vmaAllocator = VmaAllocator.Null;
    _textureManager = null!;
    _renderer = null!;
  }

  public Sprite(
    Application app,
    string? path,
    int spritesPerRow,
    int spritesPerColumn,
    bool isSpriteSheet = false,
    int flip = 1
  ) {
    _device = app.Device;
    _vmaAllocator = app.VmaAllocator;
    _textureManager = app.TextureManager;
    _renderer = app.Renderer;

    if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path), path);

    _spriteTexture = (VulkanTexture)_textureManager.AddTextureLocal(path, flip).Result;
    _textureIdRef = _textureManager.GetTextureIdLocal(_spriteTexture.TextureName);
    UsesTexture = true;
    _spritesPerRow = spritesPerRow;
    _spritesPerColumn = spritesPerColumn;
    _isSpriteSheet = isSpriteSheet;

    Init();
  }

  public Sprite(
    Application app,
    string? path,
    float spriteSheetTileSize,
    bool isSpriteSheet = false,
    int flip = 1
  ) {
    _device = app.Device;
    _vmaAllocator = app.VmaAllocator;
    _textureManager = app.TextureManager;
    _renderer = app.Renderer;

    if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path), path);

    _spriteTexture = (VulkanTexture)_textureManager.AddTextureLocal(path, flip).Result;
    _textureIdRef = _textureManager.GetTextureIdLocal(_spriteTexture.TextureName);
    UsesTexture = true;
    _spriteSheetTileSize = spriteSheetTileSize;
    _isSpriteSheet = isSpriteSheet;

    Init();
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

  public void SetSpriteTile(int spriteX, int spriteY) {
    int x = spriteX - 1;
    int y = spriteY - 1;

    if (x < 0 || x >= _spritesPerRow || y < 0 || y >= _spritesPerColumn) {
      throw new ArgumentOutOfRangeException("Sprite tile coordinates are out of range.");
    }

    // Calculate the size (in UV space) of one tile.
    float tileWidth = 1.0f / _spritesPerRow;
    float tileHeight = 1.0f / _spritesPerColumn;

    // Calculate UVs.
    float uMin = x * tileWidth;
    float uMax = uMin + tileWidth;

    // Adjust Y coordinate. Assuming texture origin is at the top-left,
    // flip the V coordinate so that y=0 is the top row.
    float vMax = 1.0f - y * tileHeight;
    float vMin = vMax - tileHeight;

    _spriteMesh.Vertices[0].Uv = new Vector2(-uMin, vMin); // Bottom-left
    _spriteMesh.Vertices[1].Uv = new Vector2(-uMin, vMax); // Top-left
    _spriteMesh.Vertices[2].Uv = new Vector2(-uMax, vMax); // Top-right
    _spriteMesh.Vertices[3].Uv = new Vector2(-uMax, vMin); // Bottom-right
  }

  private void Init() {
    GetAspectRatio();
    // if (_aspectRatio == ASPECT_ONE) {
    //   CreateSpriteVertexBox();
    // } else {
    //   CreateSpriteVertexWithAspect();
    // }

    CreateSpriteVertexBox();

    if (_isSpriteSheet) {
      if (_spritesPerRow != SPRITE_COUNT_NONE && _spritesPerColumn != SPRITE_COUNT_NONE) {
        CalculateOffset();
      } else if (_spriteSheetTileSize != SPRITE_TILE_SIZE_NONE) {
        throw new NotImplementedException("Creating sprites based on tile size is not yet implemented");
        // CalculateElemCount();
      } else {
        throw new ArgumentException("Neither of spriteCount or spriteSheetTileSize was set");
      }

      // HandleSpriteSheetUVs();
      // SetSpriteTile(1, 1);
    }

    _spriteMesh.CreateVertexBuffer();
    _spriteMesh.CreateIndexBuffer();
  }

  private void GetAspectRatio() {
    if (_spriteTexture.Width > _spriteTexture.Height) {
      _aspectRatio = (float)_spriteTexture.Height / _spriteTexture.Width;
    } else {
      _aspectRatio = (float)_spriteTexture.Width / _spriteTexture.Height;
    }
  }

  private void CalculateOffset() {
    var sizeX = (float)_spriteTexture.Width / _spritesPerRow;
    var sizeY = (float)_spriteTexture.Height / _spritesPerColumn;

    _stride.X = 1.0f / sizeX;
    _stride.Y = 1.0f / sizeY;

    // _stride = Vector2.Normalize(_stride);
  }

  private void CalculateElemCount() {

  }

  private void RecreateMesh() {
    Dispose();
    Init();
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
    Logger.Info($"STRIDE: {_stride.X} {_stride.Y}");

    SetSpriteTile(1, 1);

    // _spriteMesh.Vertices[0].Position.X /= 2;
    // _spriteMesh.Vertices[0].Uv.X = 0.0f;
    // _spriteMesh.Vertices[0].Uv.Y = 1.0f - (0.1666f * 3);

    // // _spriteMesh.Vertices[1].Position.X /= 2;
    // _spriteMesh.Vertices[1].Uv.X = 0.0f;
    // _spriteMesh.Vertices[1].Uv.Y = 1.0f;

    // // _spriteMesh.Vertices[2].Position.X /= 2;
    // _spriteMesh.Vertices[2].Uv.X = -1.0f;
    // _spriteMesh.Vertices[2].Uv.Y = 1.0f;

    // // _spriteMesh.Vertices[3].Position.X /= 2;
    // _spriteMesh.Vertices[3].Uv.X = -1.0f;
    // _spriteMesh.Vertices[3].Uv.Y = 1.0f - (0.1666f * 3);
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
  public Vector2I SpriteSheetSize => new(_spritesPerRow, _spritesPerColumn);
  public int SpriteIndex { get; set; } = 1;
}
