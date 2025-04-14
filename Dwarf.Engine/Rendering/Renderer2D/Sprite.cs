using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;

using StbImageSharp;

using Vortice.Vulkan;


namespace Dwarf.Rendering.Renderer2D;
public class Sprite {
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
    _spriteSheetTileSize = spriteSheetTileSize;
    _isSpriteSheet = isSpriteSheet;

    Init();
  }

  public void BuildDescriptors(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    _spriteTexture.BuildDescriptor(descriptorSetLayout, descriptorPool);
  }

  public void Bind(nint commandBuffer, uint index) {
    _renderer.CommandList.BindVertex(commandBuffer, _spriteMesh.VertexBuffer!, index);
    if (_spriteMesh.HasIndexBuffer) _renderer.CommandList.BindIndex(commandBuffer, _spriteMesh.IndexBuffer!, index);
  }

  public void Draw(nint commandBuffer, uint index = 0, uint firstInstance = 0) {
    if (_spriteMesh.HasIndexBuffer) {
      _renderer.CommandList.DrawIndexed(commandBuffer, _spriteMesh.IndexCount, 1, index, 0, firstInstance);
    } else {
      _renderer.CommandList.Draw(commandBuffer, _spriteMesh.VertexCount, 1, 0, 0);
    }
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

  public void Dispose() {
    _spriteMesh.Dispose();
    GC.SuppressFinalize(this);
  }
  public Guid GetTextureIdReference() {
    return _textureIdRef;
  }
  public ITexture Texture => _spriteTexture;
  public Vector2I SpriteSheetSize => new(_spritesPerRow, _spritesPerColumn);
  public int SpriteIndex { get; set; } = 1;
  public int MaxIndex {
    get {
      if (_spritesPerRow == 1) {
        return _spritesPerColumn;
      } else {
        return _spritesPerRow;
      }
    }
  }
  public Mesh SpriteMesh => _spriteMesh;
}
