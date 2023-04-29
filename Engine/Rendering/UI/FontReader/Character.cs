namespace Dwarf.Engine.Rendering.UI.FontReader;
public class Character {
  private int _id;
  private double _xTextureCoord;
  private double _yTextureCoord;
  private double _xMaxTextureCoord;
  private double _yMaxTextureCoord;
  private double _xOffset;
  private double _yOffset;
  private double _sizeX;
  private double _sizeY;
  private double _xAdvance;

  public Character(int id, double xTextureCoord, double yTextureCoord, double xMaxTextureCoord, double yMaxTextureCoord, double xOffset, double yOffset, double sizeX, double sizeY, double xAdvance) {
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
  public double XTextureCoord => _xTextureCoord;
  public double YTextureCoord => _yTextureCoord;
  public double XMaxTextureCoord => _xMaxTextureCoord;
  public double YMaxTextureCoord => _yMaxTextureCoord;
  public double XOffset => _xOffset;
  public double YOffset => _yOffset;
  public double SizeX => _sizeX;
  public double SizeY => _sizeY;
  public double XAdvance => _xAdvance;
}
