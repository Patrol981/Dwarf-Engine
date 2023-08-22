using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;

using JoltPhysicsSharp;

namespace Dwarf.Engine.Rendering.UI;
public unsafe class Canvas : Component {
  private readonly Window _window;
  private readonly Application _application;

  private Vector2 _maxCanvasSize = Vector2.Zero;
  private float _globalScale = 0.1f;

  private List<Entity> _entities = new();

  public Canvas() {
    _window = ApplicationState.Instance.Window;
    _application = ApplicationState.Instance;

    _maxCanvasSize = new Vector2(_window.Size.X, _window.Size.Y);
  }

  public void Update() {
    _maxCanvasSize = new Vector2(_window.Size.X, _window.Size.Y);

    foreach (var entity in _entities) {
      entity.GetComponent<Transform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
      // Logger.Info($"[POS] {entity.GetComponent<Transform>().Position}");
    }

    var mousePos = MouseState.GetInstance().MousePosition;
    // var entities = _application.GetEntities();
    // var targetEntities = Entity.DistinctInterface<IUIElement>(entities);
    // var transform = targetEntities[0].GetComponent<Transform>();

    // Logger.Info(transform.Matrix4.ToString());
    // Logger.Info($"[ui elem name] {targetEntities[0].Name}");
    // Logger.Info($"[mouse pos] {mousePos.X} {mousePos.Y}");
  }

  public void AddUI(Entity entity) {
    _entities.Add(entity);
  }

  public void RemoveUI(Entity entity) {
    _entities.Remove(entity);
  }
}
