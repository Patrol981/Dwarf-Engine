using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwarf.Engine.Rendering.UI;
public class ButtonMonitor : IObservable<Button> {
  private List<IObserver<Button>> _buttons;

  public ButtonMonitor() {
    _buttons = new List<IObserver<Button>>();
  }

  public IDisposable Subscribe(IObserver<Button> observer) {
    throw new NotImplementedException();
  }

  public List<IObserver<Button>> Buttons {
    get { return _buttons; }
    set { _buttons = value; }
  }
}
