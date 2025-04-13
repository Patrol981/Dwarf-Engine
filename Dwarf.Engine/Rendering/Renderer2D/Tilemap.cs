using System.Numerics;
using Dwarf;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Vulkan;

namespace Dwarf.Rendering.Renderer2D;

public class Tilemap : Component, IDrawable2D {
  private readonly Application _application;

  public Vector2I TilemapSize { get; private set; }
  public int TileSize { get; private set; }
  private int _texutresPerRow;
  public TileInfo[,] Tiles { get; set; }


  private Mesh _tilemapMesh = null!;
  private VulkanTexture? _tilemapAtlas;

  public Entity Entity => Owner;
  public bool Active => Owner.Active;
  public ITexture Texture => _tilemapAtlas ?? null!;

  public Tilemap() {
    _application = Application.Instance;
    TilemapSize = new Vector2I(0, 0);
    Tiles = new TileInfo[TilemapSize.X, TilemapSize.Y];
  }

  public Tilemap(Application app, Vector2I tileMapSize, string textureAtlasPath, int tileSize) {
    _application = app;
    TilemapSize = tileMapSize;
    TileSize = tileSize;
    Tiles = new TileInfo[TilemapSize.X, TilemapSize.Y];

    _tilemapAtlas = (VulkanTexture)app.TextureManager.AddTextureLocal(textureAtlasPath).Result;

    _texutresPerRow = (int)MathF.Floor(_tilemapAtlas.Width / TileSize);
  }

  public void SetTile(int x, int y, TileInfo tileInfo) {
    if (Tiles.IsWithinTilemap(x, y)) {
      Tiles[x, y].X = tileInfo.X;
      Tiles[x, y].Y = tileInfo.X;
      Tiles[x, y].TextureX = tileInfo.TextureX;
      Tiles[x, y].TextureY = tileInfo.TextureY;
      Tiles[x, y].IsNotEmpty = tileInfo.IsNotEmpty;
    } else {
      throw new IndexOutOfRangeException("Attempted to set tile outside of timemap range");
    }
  }

  public TileInfo? GetTile(int x, int y) {
    if (Tiles.IsWithinTilemap(x, y)) {
      return Tiles[x, y];
    }
    return null;
  }

  public Task Bind(nint commandBuffer, uint index) {
    if (_tilemapMesh.VertexBuffer == null) return Task.CompletedTask;

    _application.Renderer.CommandList.BindVertex(commandBuffer, _tilemapMesh.VertexBuffer, 0);
    if (_tilemapMesh.IndexBuffer != null) {
      _application.Renderer.CommandList.BindIndex(commandBuffer, _tilemapMesh.IndexBuffer, 0);
    }

    return Task.CompletedTask;
  }

  public Task Draw(nint commandBuffer, uint index, uint firstInstance) {
    if (_tilemapMesh.VertexBuffer == null) return Task.CompletedTask;

    if (_tilemapMesh.HasIndexBuffer) {
      _application.Renderer.CommandList.DrawIndexed(commandBuffer, _tilemapMesh.IndexCount, 1, 0, 0, 0);
    } else {
      _application.Renderer.CommandList.Draw(commandBuffer, _tilemapMesh.IndexCount, 1, 0, 0);
    }

    return Task.CompletedTask;
  }

  public void BuildDescriptors(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    _tilemapAtlas?.BuildDescriptor(descriptorSetLayout, descriptorPool);
  }

  public void CreateTilemap() {
    GenerateMesh();
  }

  private (float, float, float, float) GetUVCoords(TileInfo tileInfo) {
    int row = tileInfo.TextureY;
    int col = tileInfo.TextureX;

    float uvSize = 1.0f / TileSize;
    float uMin = col * uvSize;
    float vMin = 1.0f - (row + 1) * uvSize;
    float uMax = (col + 1) * uvSize;
    float vMax = 1.0f - row * uvSize;

    (vMax, vMin) = (vMin, vMax);

    return (uMin, uMax, vMin, vMax);
  }

  private void GenerateMesh() {
    _tilemapMesh = new(_application.VmaAllocator, _application.Device);

    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    var atlasWidth = _tilemapAtlas?.Width ?? 256;
    var atlasHeight = _tilemapAtlas?.Height ?? 256;

    float worldTileSize = 0.10f;

    for (uint y = 0; y < TilemapSize.Y; y++) {
      for (uint x = 0; x < TilemapSize.X; x++) {
        var tileInfo = Tiles[x, y];

        if (!tileInfo.IsNotEmpty) continue;

        float posX = x * worldTileSize;
        float posY = y * worldTileSize;

        // float uMin = tileInfo.TextureX * TileSize / atlasWidth;
        // float vMin = tileInfo.TextureY * TileSize / atlasHeight;
        // float uMax = (tileInfo.TextureX + 1) * TileSize / atlasWidth;
        // float vMax = (tileInfo.TextureY + 1) * TileSize / atlasHeight;

        // uMin = 0;
        // uMax = -1;
        // vMax = -1;
        // vMin = 0;

        if (tileInfo.HasUVCoords()) {
          var bottomLeft = new Vertex {
            Position = new Vector3(posX, posY, 0.0f),
            Uv = new Vector2(tileInfo.UMin, tileInfo.VMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var bottomRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY, 0.0f),
            Uv = new Vector2(tileInfo.UMax, tileInfo.VMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY + worldTileSize, 0.0f),
            Uv = new Vector2(tileInfo.UMax, tileInfo.VMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topLeft = new Vertex {
            Position = new Vector3(posX, posY + worldTileSize, 0.0f),
            Uv = new Vector2(tileInfo.UMin, tileInfo.VMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };

          uint baseIndex = (uint)vertices.Count;
          vertices.Add(bottomLeft);
          vertices.Add(bottomRight);
          vertices.Add(topRight);
          vertices.Add(topLeft);

          // Two triangles per quad, using counter-clockwise winding (adjust if needed).
          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 1);
          indices.Add(baseIndex + 2);

          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 2);
          indices.Add(baseIndex + 3);
        } else {
          var (uMin, uMax, vMin, vMax) = GetUVCoords(tileInfo);
          Logger.Info("GETTING UV COORDS");

          var bottomLeft = new Vertex {
            Position = new Vector3(posX, posY, 0.0f),
            Uv = new Vector2(uMin, vMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var bottomRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY, 0.0f),
            Uv = new Vector2(uMax, vMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY + worldTileSize, 0.0f),
            Uv = new Vector2(uMax, vMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topLeft = new Vertex {
            Position = new Vector3(posX, posY + worldTileSize, 0.0f),
            Uv = new Vector2(uMin, vMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };

          uint baseIndex = (uint)vertices.Count;
          vertices.Add(bottomLeft);
          vertices.Add(bottomRight);
          vertices.Add(topRight);
          vertices.Add(topLeft);

          // Two triangles per quad, using counter-clockwise winding (adjust if needed).
          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 1);
          indices.Add(baseIndex + 2);

          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 2);
          indices.Add(baseIndex + 3);
        }
      }
    }

    _tilemapMesh.VertexCount = (ulong)vertices.Count;
    _tilemapMesh.IndexCount = (ulong)indices.Count;

    _tilemapMesh.Vertices = [.. vertices];
    _tilemapMesh.Indices = [.. indices];

    _tilemapMesh.CreateVertexBuffer();
    _tilemapMesh.CreateIndexBuffer();
  }

  public void Dispose() {
    _tilemapMesh.Dispose();
    GC.SuppressFinalize(this);
  }
}