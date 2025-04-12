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
  public uint TileSize { get; private set; }
  private TileInfo[,] _tiles;


  private Mesh _tilemapMesh = null!;
  private VulkanTexture? _tilemapAtlas;

  public Entity Entity => Owner;
  public bool Active => Owner.Active;
  public ITexture Texture => _tilemapAtlas ?? null!;

  public Tilemap() {
    _application = Application.Instance;
    TilemapSize = new Vector2I(0, 0);
    _tiles = new TileInfo[TilemapSize.X, TilemapSize.Y];
  }

  public Tilemap(Application app, Vector2I tileMapSize, string textureAtlasPath, uint tileSize) {
    _application = app;
    TilemapSize = tileMapSize;
    TileSize = tileSize;
    _tiles = new TileInfo[TilemapSize.X, TilemapSize.Y];

    _tilemapAtlas = (VulkanTexture)app.TextureManager.AddTextureLocal(textureAtlasPath).Result;

    CreateTilemap();
  }

  public void SetTile(uint x, uint y, TileInfo tileInfo) {
    if (_tiles.IsWithinTilemap(x, y)) {
      _tiles[x, y] = tileInfo;
    }
  }

  public TileInfo? GetTile(uint x, uint y) {
    if (_tiles.IsWithinTilemap(x, y)) {
      return _tiles[x, y];
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

  private void CreateTilemap() {
    GenerateMesh();
  }

  private void GenerateMesh() {
    _tilemapMesh = new(_application.VmaAllocator, _application.Device);

    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    var atlasWidth = _tilemapAtlas?.Width ?? 256;
    var atlasHeight = _tilemapAtlas?.Height ?? 256;

    for (uint y = 0; y < TilemapSize.Y; y++) {
      for (uint x = 0; x < TilemapSize.X; x++) {
        var tileInfo = _tiles[x, y];

        if (tileInfo.IsEmpty) continue;

        var posX = x * TileSize;
        var posY = y * TileSize;

        float uMin = tileInfo.TextureX * TileSize / atlasWidth;
        float vMin = tileInfo.TextureY * TileSize / atlasHeight;
        float uMax = (tileInfo.TextureX + 1) * TileSize / atlasWidth;
        float vMax = (tileInfo.TextureY + 1) * TileSize / atlasHeight;

        var bottomLeft = new Vertex {
          Position = new Vector3(posX, posY, 0.0f),
          Uv = new Vector2(uMin, vMin),
          Color = new Vector3(1, 1, 1),
          Normal = new Vector3(0, 0, 1)
        };
        var bottomRight = new Vertex {
          Position = new Vector3(posX + TileSize, posY, 0.0f),
          Uv = new Vector2(uMax, vMin),
          Color = new Vector3(1, 1, 1),
          Normal = new Vector3(0, 0, 1)
        };
        var topRight = new Vertex {
          Position = new Vector3(posX + TileSize, posY + TileSize, 0.0f),
          Uv = new Vector2(uMax, vMax),
          Color = new Vector3(1, 1, 1),
          Normal = new Vector3(0, 0, 1)
        };
        var topLeft = new Vertex {
          Position = new Vector3(posX, posY + TileSize, 0.0f),
          Uv = new Vector2(uMin, vMax),
          Color = new Vector3(1, 1, 1),
          Normal = new Vector3(0, 0, 1)
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