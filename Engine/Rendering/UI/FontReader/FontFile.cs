using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Extensions.Logging;

using Vortice.Vulkan;

namespace Dwarf.Engine.Rendering.UI.FontReader;

public enum MetaFilePadding {
  Top = 0,
  Left = 1,
  Bottom = 2,
  Right = 3
}

public class FontFile {
  private const MetaFilePadding DesiredPadding = MetaFilePadding.Right;
  private const string Splitter = " ";
  private const string NumberSeparator = ",";

  private Dictionary<string, string> _values = new();
  private Dictionary<int, Character> _metaData = new();

  private int[] _padding = Array.Empty<int>();
  private int _paddingWidth;
  private int _paddingHeight;

  private float _verticalPerPixelSize;
  private float _horizontalPerPixelSize;
  private float _spaceWidth;

  private float _aspect;
  private FileStream _file = null!;
  private StreamReader _reader = StreamReader.Null;

  public FontFile(VkExtent2D innerSize, string fileName) {
    _aspect = innerSize.width / innerSize.height;
    ReadFile(fileName);
    LoadPaddingData();
    LoadLineSizes();
    int imgWidth = GetValueOfVariable("scaleW");
    LoadCharacterData(imgWidth);
    Close();
  }

  private int GetValueOfVariable(string variable) {
    return int.Parse(_values[variable]);
  }

  private int[] GetValuesOfVariable(string variable) {
    var stringNumbers = _values[variable].Split(NumberSeparator);
    var numbers = new int[stringNumbers.Length];

    for (uint i = 0; i < numbers.Length; i++) {
      numbers[i] = int.Parse(stringNumbers[i]);
    }
    return numbers;
  }

  private Character LoadCharacter(int imgSize) {
    int id = GetValueOfVariable("id");
    if (id == (int)TextMeshCreator.SpaceAscii) {
      _spaceWidth = (GetValueOfVariable("xadvance") - _paddingWidth) * _horizontalPerPixelSize;
      return null!;
    }
    float xTex =
      ((float)GetValueOfVariable("x") +
      (_padding[(int)MetaFilePadding.Left] - (int)DesiredPadding))
      / imgSize;
    float yTex =
      ((float)GetValueOfVariable("y") +
      (_padding[(int)MetaFilePadding.Top] - (int)DesiredPadding))
      / imgSize;
    int width = GetValueOfVariable("width") - (_paddingWidth - (2 * (int)DesiredPadding));
    int height = GetValueOfVariable("height") - (_paddingHeight - (2 * (int)DesiredPadding));
    float quadWidth = width * _horizontalPerPixelSize;
    float quadHeight = height * _verticalPerPixelSize;
    float xTexSize = (float)width / imgSize;
    float yTexSize = (float)height / imgSize;
    float xOffset =
      (GetValueOfVariable("xoffset") + _padding[(int)MetaFilePadding.Left] - (int)DesiredPadding)
      * _horizontalPerPixelSize;
    float yOffset =
      (GetValueOfVariable("yoffset") + _padding[(int)MetaFilePadding.Top] - (int)DesiredPadding)
      * _verticalPerPixelSize;
    float xAdvance = (GetValueOfVariable("xadvance") - _paddingWidth) * _horizontalPerPixelSize;

    return new Character(id, xTex, yTex, xTexSize, yTexSize, xOffset, yOffset, quadWidth, quadHeight, xAdvance);
  }

  private bool ProcessNextLine() {
    _values.Clear();
    string line = _reader.ReadLine()!;
    if (line == null) return false;

    foreach (var part in line.Split(Splitter)) {
      string[] valuePairs = part.Split("=");
      if (valuePairs.Length == 2) {
        _values.Add(valuePairs[0], valuePairs[1]);
      }
    }
    return true;
  }

  private void ReadFile(string fileName) {
    string fontPath = $"./Fonts/{fileName}";

    _file = File.OpenRead(fontPath);
    _reader = new StreamReader(_file);
  }

  private void LoadPaddingData() {
    ProcessNextLine();
    _padding = GetValuesOfVariable("padding");
    _paddingWidth = _padding[(int)MetaFilePadding.Left] + _padding[(int)MetaFilePadding.Right];
    _paddingHeight = _padding[(int)MetaFilePadding.Top] + _padding[(int)MetaFilePadding.Bottom];
  }

  private void LoadLineSizes() {
    ProcessNextLine();
    int lineHeightPixels = GetValueOfVariable("lineHeight") - _paddingHeight;
    _verticalPerPixelSize = TextMeshCreator.LineHeight / lineHeightPixels;
    _horizontalPerPixelSize = _verticalPerPixelSize / _aspect;
  }

  private void LoadCharacterData(int imgWidth) {
    ProcessNextLine();
    ProcessNextLine();
    while (ProcessNextLine()) {
      Character character = LoadCharacter(imgWidth);
      if (character != null) {
        _metaData.Add(character.Id, character);
      }
    }
  }

  private void Close() {
    _file.Close();
    _reader.Dispose();
  }

  public float SpaceWidth => _spaceWidth;
  public Character GetCharacter(int ascii) {
    return _metaData[ascii];
  }
}
