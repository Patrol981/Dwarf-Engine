using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwarf.Engine.Rendering.UI.FontReader;
public class Line {
  private float _maxLength;
  private float _spaceSize;

  private List<Word> _words = new();
  private float _currentLineLength = 0;

  public Line(float spaceWidth, float fontSize, float maxLength) {
    _spaceSize = spaceWidth * fontSize;
    _maxLength = maxLength;
  }

  public bool TryAddWord(Word word) {
    float additionalLength = word.WordWidth;
    additionalLength += _words.Count > 0 ? _spaceSize : 0;
    if (_currentLineLength + additionalLength <= _maxLength) {
      _words.Add(word);
      _currentLineLength += additionalLength;
      return true;
    }
    return false;
  }

  public float MaxLength => _maxLength;
  public float LineLength => _currentLineLength;
  public List<Word> Words => _words;
}
