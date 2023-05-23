using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwarf.Engine.Rendering.UI.FontReader;
public class Word {
  private List<Character> _characters = new();
  private float _width = 0;
  private float _fontSize;

  public Word(float fontSize) {
    _fontSize = fontSize;
  }

  public void AddCharacter(Character character) {
    _characters.Add(character);
    _width += character.XAdvance * _fontSize;
  }

  public List<Character> Characters => _characters;
  public float WordWidth => _width;
}
