using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Assimp.Unmanaged;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;

using DwarfEngine.Engine.Rendering.UI;

using JoltPhysicsSharp;

namespace Dwarf.Engine.Rendering.UI;

public enum Anchor {
  StickToRight,
  StickToLeft,
  StickToMiddle,
  StickToBottom,
  StickToTop
}

public enum ResolutionAspect {
  Aspect4to3,
  Aspect8to5,
  Aspect16to9,
  Aspect21to9
}

public class Resolution {
  public Vector2 Size { get; private set; }
  public ResolutionSize ResolutionSize { get; private set; }
  public ResolutionAspect ResolutionAspect { get; private set; }

  public Resolution(Vector2 size, ResolutionSize resolutionSize, ResolutionAspect resolutionAspect) {
    Size = size;
    ResolutionSize = resolutionSize;
    ResolutionAspect = resolutionAspect;
  }
}

public enum ResolutionSize {
  Screen800x600,
  Screen1024x600,
  Screen1334x750,
  Screen1280x800,
  Screen1600x900,
  Screen1920x1080,
  Screen2560x1080
}

public unsafe class Canvas : Component, IDisposable {
  private readonly Window _window;
  private readonly Application _application;

  private Vector2 _maxCanvasSize = Vector2.Zero;
  private float _globalScale = 0.1f;
  private Resolution[] _resolutions = {
    new Resolution(new(800, 600), ResolutionSize.Screen800x600, ResolutionAspect.Aspect4to3),
    new Resolution(new(1024, 600), ResolutionSize.Screen1024x600, ResolutionAspect.Aspect16to9),
    new Resolution(new(1334, 750), ResolutionSize.Screen1334x750, ResolutionAspect.Aspect16to9),
    new Resolution(new(1280, 800), ResolutionSize.Screen1280x800, ResolutionAspect.Aspect8to5),
    new Resolution(new(1600, 900), ResolutionSize.Screen1600x900, ResolutionAspect.Aspect16to9),
    new Resolution(new(1920, 1080), ResolutionSize.Screen1920x1080, ResolutionAspect.Aspect16to9),
    new Resolution(new(2560, 1080), ResolutionSize.Screen2560x1080, ResolutionAspect.Aspect21to9),
  };

  private List<Entity> _entities = new();

  public Canvas() {
    _window = ApplicationState.Instance.Window;
    _application = ApplicationState.Instance;

    _maxCanvasSize = new Vector2(_window.Size.X, _window.Size.Y);
  }

  public void Update() {
    // _maxCanvasSize = new Vector2(_window.Size.X, _window.Size.Y);
    CheckResolution();

    foreach (var entity in _entities) {
      entity.GetComponent<RectTransform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
      // Logger.Info($"[POS] {entity.GetComponent<Transform>().Position}");
    }

    // Logger.Info($"[Current global scale] {_globalScale}");
    // Logger.Info($"[Window size] {_window.Extent.width} {_window.Extent.height}");

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

  public Span<Entity> GetUI() {
    return _entities.ToArray();
  }

  public Entity CreateButton(
    string texturePath,
    Anchor anchor = Anchor.StickToMiddle,
    Vector2 offsetFromAnchor = new Vector2(),
    string buttonName = "button"
  ) {
    var button = new Entity();
    button.AddComponent(new RectTransform(new Vector3(0f, 0f, 0f)));
    button.GetComponent<RectTransform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
    button.GetComponent<RectTransform>().Rotation = new Vector3(0, 0, 180);
    button.GetComponent<RectTransform>().Anchor = anchor;
    button.GetComponent<RectTransform>().OffsetFromVector = offsetFromAnchor;
    button.AddComponent(new GuiTexture(_application.Device));
    button.GetComponent<GuiTexture>().BindToTexture(_application.TextureManager, texturePath, false);
    button.Name = buttonName;
    _entities.Add(button);
    return button;
  }

  public Entity CreateImage(
    string texturePath,
    Anchor anchor = Anchor.StickToMiddle,
    Vector2 offsetFromAnchor = new Vector2(),
    string imageName = "image"
  ) {
    var image = new Entity();
    image.AddComponent(new RectTransform(new Vector3(0f, 0f, 0f)));
    image.GetComponent<RectTransform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
    image.GetComponent<RectTransform>().Rotation = new Vector3(0, 0, 180);
    image.GetComponent<RectTransform>().Anchor = anchor;
    image.GetComponent<RectTransform>().OffsetFromVector = offsetFromAnchor;
    image.AddComponent(new GuiTexture(_application.Device));
    image.GetComponent<GuiTexture>().BindToTexture(_application.TextureManager, texturePath, false);
    image.Name = imageName;
    _entities.Add(image);
    return image;
  }

  public Entity CreateText(
    string textData,
    Anchor anchor = Anchor.StickToMiddle,
    Vector2 offsetFromAnchor = new Vector2(),
    string textName = "text"
  ) {
    var text = new Entity();
    text.AddComponent(new RectTransform(new Vector3(0f, 0f, 0f)));
    text.GetComponent<RectTransform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
    text.GetComponent<RectTransform>().Rotation = new Vector3(0, 0, 180);
    text.GetComponent<RectTransform>().Anchor = anchor;
    text.GetComponent<RectTransform>().OffsetFromVector = offsetFromAnchor;
    text.AddComponent(new TextField(_application, textData));
    text.GetComponent<TextField>().BindToTexture(_application.TextureManager, "./Fonts/atlas.png");
    text.GetComponent<TextField>().Init();
    text.Name = textName;
    _entities.Add(text);
    return text;
  }

  private void CheckResolution() {
    if (_maxCanvasSize.X == _window.Extent.width && _maxCanvasSize.Y == _window.Extent.height) return;

    _maxCanvasSize = new Vector2(_window.Extent.width, _window.Extent.height);
    var minDistance = float.MaxValue;
    Resolution closestRes = null!;

    // find closest resolution
    foreach (var res in _resolutions) {
      var distance = Vector2.Distance(_maxCanvasSize, res.Size);
      if (distance < minDistance) {
        minDistance = distance;
        closestRes = res;
      }
    }

    switch (closestRes.ResolutionSize) {
      case ResolutionSize.Screen800x600:
        _globalScale = 0.1f;
        break;
      case ResolutionSize.Screen1024x600:
        _globalScale = 0.15f;
        break;
      case ResolutionSize.Screen1334x750:
        _globalScale = 0.15f;
        break;
      case ResolutionSize.Screen1280x800:
        _globalScale = 0.15f;
        break;
      case ResolutionSize.Screen1600x900:
        _globalScale = 0.2f;
        break;
      case ResolutionSize.Screen1920x1080:
        _globalScale = 0.25f;
        break;
      case ResolutionSize.Screen2560x1080:
        _globalScale = 0.25f;
        break;
    }

    Logger.Info($"[Closest res] {closestRes.Size}");
    Logger.Info($"[Global scale] {_globalScale}");
  }

  public void Dispose() {
    foreach (var e in _entities) {
      e?.DisposeEverything();
    }
  }

  // private Vector2 Calculate
}
