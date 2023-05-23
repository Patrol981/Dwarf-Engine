namespace Dwarf.Engine.Rendering.UI.FontReader;
public class Character {
  private int _id;
  private float _xTextureCoord;
  private float _yTextureCoord;
  private float _xMaxTextureCoord;
  private float _yMaxTextureCoord;
  private float _xOffset;
  private float _yOffset;
  private float _sizeX;
  private float _sizeY;
  private float _xAdvance;

  public Character(int id, float xTextureCoord, float yTextureCoord, float xMaxTextureCoord, float yMaxTextureCoord, float xOffset, float yOffset, float sizeX, float sizeY, float xAdvance) {
    _id = id;
    _xTextureCoord = xTextureCoord;
    _yTextureCoord = yTextureCoord;
    _xMaxTextureCoord = xMaxTextureCoord;
    _yMaxTextureCoord = yMaxTextureCoord;
    _xOffset = xOffset;
    _yOffset = yOffset;
    _sizeX = sizeX;
    _sizeY = sizeY;
    _xAdvance = xAdvance;
  }

  public int Id => _id;
  public float XTextureCoord => _xTextureCoord;
  public float YTextureCoord => _yTextureCoord;
  public float XMaxTextureCoord => _xMaxTextureCoord;
  public float YMaxTextureCoord => _yMaxTextureCoord;
  public float XOffset => _xOffset;
  public float YOffset => _yOffset;
  public float SizeX => _sizeX;
  public float SizeY => _sizeY;
  public float XAdvance => _xAdvance;
}
