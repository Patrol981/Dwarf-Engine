namespace Dwarf.Rendering.Renderer2D;

public static class TilemapHelpers {
  public static bool IsWithinTilemap(this TileInfo[,] tiles, uint x, uint y) {
    if (tiles.GetLength(0) < x || tiles.GetLength(1) < y) return false;
    return true;
  }
}