using DotTiled;
using DotTiled.Serialization;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Renderer2D;
using Dwarf.Utils;

namespace Dwarf.Loaders.Tiled;

public static class TiledLoader {
  public static Tilemap LoadTilemap(Application app, string tmxPath, string textureAtlasPath) {
    var loader = Loader.Default();
    var map = loader.LoadMap(Path.Combine(DwarfPath.AssemblyDirectory, tmxPath));

    if (map.Infinite) throw new NotSupportedException("Loader does not support infinite maps");
    if (map.Layers.Count > 1) throw new NotSupportedException("Loader does not support multiple layers");
    if (map.Layers[0] is not DotTiled.TileLayer tileLayer) throw new ArgumentException("No tile layer found"); ;

    var tilemap = new Tilemap(app, new((int)map.Width, (int)map.Height), textureAtlasPath, (int)map.TileHeight);

    for (int y = 0; y < tileLayer.Height; y++) {
      for (int x = 0; x < tileLayer.Width; x++) {
        var index = x + y * (int)tileLayer.Width;
        var tile = tileLayer.Data.Value.GlobalTileIDs.Value[index];

        var tileInfo = new TileInfo {
          X = x,
          Y = y,
        };

        if (tile == 0) {
          tileInfo.IsNotEmpty = false;
          tileInfo.TextureX = -1;
          tileInfo.TextureY = -1;
          tileInfo.UMin = 0f;
          tileInfo.UMax = 0f;
          tileInfo.VMin = 0f;
          tileInfo.VMax = 0f;
        } else {
          tileInfo.IsNotEmpty = true;

          Tileset match = null!;
          foreach (var tileset in map.Tilesets) {
            if (tile >= tileset.FirstGID)
              match = tileset;
            else
              break;
          }

          if (match == null) continue;

          var localId = tile - match.FirstGID;

          // Tileset properties
          var margin = match.Margin;
          var spacing = match.Spacing;
          var tileWidth = match.TileWidth;
          var tileHeight = match.TileHeight;
          var imageWidth = match.Image.Value.Width;
          var imageHeight = match.Image.Value.Height;

          // Calculate the number of tiles per row on the tileset image.
          var tilesPerRow = (imageWidth - 2 * margin + spacing) / (tileWidth + spacing);

          // Calculate column and row within the tileset.
          var tileCol = localId % tilesPerRow;
          var tileRow = localId / tilesPerRow;

          // Calculate texture position in pixels.
          var textureX = margin + tileCol * (tileWidth + spacing);
          var textureY = margin + tileRow * (tileHeight + spacing);
          tileInfo.TextureX = (int)textureX;
          tileInfo.TextureY = (int)textureY;

          // Compute normalized UV coordinates.
          tileInfo.UMin = (float)textureX / imageWidth;
          tileInfo.UMax = (float)(textureX + tileWidth) / imageWidth;
          tileInfo.VMin = (float)textureY / imageHeight;
          tileInfo.VMax = (float)(textureY + tileHeight) / imageHeight;

          // If your graphics system uses a bottom-left origin for textures,
          // you may need to invert the V coordinates. For example:
          // float tempVMin = tileInfo.VMin;
          // tileInfo.VMin = 1.0f - tileInfo.VMax;
          // tileInfo.VMax = 1.0f - tempVMin;

          tileInfo.VMin = -tileInfo.VMin;
          tileInfo.VMax = -tileInfo.VMax;

          tilemap.Tiles[x, y] = tileInfo;
        }
      }
    }

    tilemap.CreateTilemap();
    return tilemap;
  }
}