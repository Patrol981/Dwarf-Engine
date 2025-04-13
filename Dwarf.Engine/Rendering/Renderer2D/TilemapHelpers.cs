namespace Dwarf.Rendering.Renderer2D;

public static class TilemapHelpers {
  public static bool IsWithinTilemap(this TileInfo[,] tiles, int x, int y) {
    if (tiles.GetLength(0) < x || tiles.GetLength(1) < y) return false;
    return true;
  }

  public static bool HasUVCoords(this TileInfo tileInfo) {
    return tileInfo.UMin > 0 || tileInfo.UMax > 0 || tileInfo.VMin > 0 || tileInfo.VMax > 0;
  }
}