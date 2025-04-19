using System.Numerics;
using Dwarf.Math;
using Dwarf.Rendering.Renderer2D.Components;
using Dwarf.Rendering.Renderer2D.Models;

namespace Dwarf.Rendering.Renderer2D.Helpers;

public static class TilemapHelpers {
  public static bool IsWithinTilemap(this TileInfo[,] tiles, int x, int y) {
    if (tiles.GetLength(0) < x || tiles.GetLength(1) < y) return false;
    return true;
  }

  public static bool HasUVCoords(this TileInfo tileInfo) {
    return tileInfo.UMin > 0 || tileInfo.UMax > 0 || tileInfo.VMin > 0 || tileInfo.VMax > 0;
  }

  public static List<Edge> ExtractEgdges(this Tilemap tilemap) {
    var collTimemap = tilemap.Layers.Where(x => x.IsCollision).First();
    int w = collTimemap.Tiles.GetLength(0), h = collTimemap.Tiles.GetLength(1);
    var tileSize = tilemap.TileSize;
    var edges = new List<Edge>();

    void AddEdge(int x1, int y1, int x2, int y2) {
      var A = new Vector2(x1, y1) * tileSize;
      var B = new Vector2(x2, y2) * tileSize;
      // Edge direction
      var dir = Vector2.Normalize(B - A);
      // Normal = rotate ninety degrees:
      var normal = new Vector2(-dir.Y, dir.X);
      edges.Add(new Edge { A = A, B = B, Normal = normal });
    }

    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (!collTimemap.Tiles[x, y].IsCollision) continue;
        // Check each of the 4 cardinal neighbors:
        if (y + 1 >= h || !collTimemap.Tiles[x, y + 1].IsCollision) AddEdge(x, y + 1, x + 1, y + 1); // top
        if (y - 1 < 0 || !collTimemap.Tiles[x, y - 1].IsCollision) AddEdge(x + 1, y, x, y);     // bottom
        if (x - 1 < 0 || !collTimemap.Tiles[x - 1, y].IsCollision) AddEdge(x, y, x, y + 1);     // left
        if (x + 1 >= w || !collTimemap.Tiles[x + 1, y].IsCollision) AddEdge(x + 1, y + 1, x + 1, y);   // right
      }
    }

    return edges;
  }
}