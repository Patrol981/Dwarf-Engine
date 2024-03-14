namespace Dwarf.Engine.Rendering.UI;

using System.Numerics;

using Dwarf.Extensions.Logging;
using Dwarf.Utils;
using Dwarf.Vulkan;

using FreeTypeSharp;
using FreeTypeSharp.Native;
// using FreeTypeSharp.Native;
using static FreeTypeSharp.Native.FT;

public struct Character {
  public VulkanTexture Texture;
  public Vector2 Size;
  public Vector2 Bearing;
  public uint Advance;
}

public class FreeType {
  private readonly VulkanDevice _device;

  public FreeType(VulkanDevice device) {
    _device = device;
  }

  public unsafe void Init() {
    var ftLibrary = new FreeTypeLibrary();
    if (FT_Init_FreeType(out var pointer) != FT_Error.FT_Err_Ok) {
      Logger.Error($"[FREETYPE] Failed to load library");
      return;
    }
    if (FT_New_Face(ftLibrary.Native, $"{DwarfPath.AssemblyDirectory}/Resources/fonts/DroidSans.ttf", 0, out var face) != FT_Error.FT_Err_Ok) {
      Logger.Error($"[FREETYPE] Failed to load face");
      return;
    }

    FT_Set_Pixel_Sizes(face, 0, 48);
    if (FT_Load_Char(face, 'X', FT_LOAD_RENDER) != FT_Error.FT_Err_Ok) {
      Logger.Error($"[FREETYPE] Failed to load glyph");
      return;
    }

    var targetFace = new FreeTypeFaceFacade(ftLibrary, face);
    // isnt the face a image that contains all of the characters?
    // todo: check how to implement FreeType with Vulkan, OpenGL version seems sussy

    for (char c = (char)65; c < 70; c++) {
      if (FT_Load_Char(face, c, FT_LOAD_RENDER) != FT_Error.FT_Err_Ok) {
        Logger.Error($"[FREETYPE] Failed to load glyph {c}");
        continue;
      }

      Console.WriteLine(c);

      var texture = new VulkanTexture(
        _device,
        (int)targetFace.GlyphBitmap.width,
        (int)targetFace.GlyphBitmap.rows,
        c.ToString()
      );

      // var buff = targetFace.GlyphBitmap.buffer;
      // texture.SetTextureData(buff);

      Character character = new() {
        Texture = texture,
        Size = new(targetFace.GlyphBitmap.width, targetFace.GlyphBitmap.rows),
        Bearing = new(targetFace.GlyphBitmapLeft, targetFace.GlyphBitmapTop),
        Advance = (uint)targetFace.GlyphMetricHorizontalAdvance
      };

      Characters.Add(c, character);
    }

    FT_Done_Face(face);
    FT_Done_FreeType(ftLibrary.Native);
  }

  public Dictionary<char, Character> Characters { get; } = [];
}