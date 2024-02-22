using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Assimp.Unmanaged;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Math;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;

using Dwarf.Engine.Rendering.UI;

using JoltPhysicsSharp;

namespace Dwarf.Engine.Rendering.UI;

public enum Anchor {
  Right,
  Left,
  Middle,
  Bottom,
  Top,
  RightTop,
  RightBottom,
  LeftTop,
  LeftBottom,
  MiddleTop,
  MiddleBottom
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

public class Canvas : Component, IDisposable {
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
  private Resolution _currentResoltionScale;

  private List<Entity> _entities = new();

  public Canvas() {
    _window = Application.Instance.Window;
    _application = Application.Instance;

    _maxCanvasSize = new Vector2(_window.Size.X, _window.Size.Y);
    _currentResoltionScale = null!;
    CheckResolution();
  }

  public async void Update() {
    await CheckResolution();

    foreach (var entity in _entities) {
      if (entity.HasComponent<FreeTypeText>()) {
        continue;
      }
      var rect = entity.GetComponent<RectTransform>();
      await CheckScale(rect);
      await CheckAnchor(rect);
      rect.RequireUpdate = false;
    }
  }

  public void AddUI(Entity entity) {
    _entities.Add(entity);
  }

  public void RemoveUI(Entity entity) {
    _entities.Remove(entity);
  }

  public Span<Entity> GetUI() {
    if (_entities.Count < 1) return new Entity[] { };
    return _entities.ToArray();
  }

  public Entity CreateButton(
    string texturePath,
    Anchor anchor = Anchor.Middle,
    Vector2 offsetFromAnchor = new Vector2(),
    string buttonName = "button",
    float originScale = 1.0f
  ) {
    var button = new Entity();
    button.AddComponent(new RectTransform(new Vector3(0f, 0f, 0f)));
    button.GetComponent<RectTransform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
    button.GetComponent<RectTransform>().Rotation = new Vector3(0, 0, 180);
    button.GetComponent<RectTransform>().Anchor = anchor;
    button.GetComponent<RectTransform>().OffsetFromVector = offsetFromAnchor;
    button.GetComponent<RectTransform>().OriginScale = originScale;
    button.AddComponent(new Button(_application, texturePath));
    button.Name = buttonName;
    _entities.Add(button);
#pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
    MouseState.GetInstance().ClickEvent += button.GetComponent<Button>().CheckCollision;
#pragma warning restore CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
    return button;
  }

  public Entity CreateImage(
    string texturePath,
    Anchor anchor = Anchor.Middle,
    Vector2 offsetFromAnchor = new Vector2(),
    string imageName = "image",
    float originScale = 1.0f
  ) {
    var image = new Entity();
    image.AddComponent(new RectTransform(new Vector3(0f, 0f, 0f)));
    image.GetComponent<RectTransform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
    image.GetComponent<RectTransform>().Rotation = new Vector3(0, 0, 180);
    image.GetComponent<RectTransform>().Anchor = anchor;
    image.GetComponent<RectTransform>().OffsetFromVector = offsetFromAnchor;
    image.GetComponent<RectTransform>().OriginScale = originScale;
    image.AddComponent(new GuiTexture(_application.Device));
    image.GetComponent<GuiTexture>().BindToTexture(_application.TextureManager, texturePath, false);
    image.Name = imageName;
    _entities.Add(image);
    return image;
  }

  public Entity CreateText(
    string textData,
    Anchor anchor = Anchor.Middle,
    Vector2 offsetFromAnchor = new Vector2(),
    string textName = "text",
    float originScale = 1.0f
  ) {
    var text = new Entity();
    text.AddComponent(new RectTransform(new Vector3(0f, 0f, 0f)));
    text.GetComponent<RectTransform>().Scale = new Vector3(_globalScale, _globalScale, 1f);
    text.GetComponent<RectTransform>().Rotation = new Vector3(0, 0, 180);
    text.GetComponent<RectTransform>().Anchor = anchor;
    text.GetComponent<RectTransform>().OffsetFromVector = offsetFromAnchor;
    text.GetComponent<RectTransform>().OriginScale = originScale;
    text.AddComponent(new TextField(_application));
    text.GetComponent<TextField>().BindToTexture(_application.TextureManager, $"{DwarfPath.AssemblyDirectory}/Resources/fonts/atlas.png");
    text.GetComponent<TextField>().Init();
    text.Name = textName;
    _entities.Add(text);
    return text;
  }

  private Task CheckScale(RectTransform rect) {
    if (rect.LastGlobalScale == _globalScale && !rect.RequireUpdate) return Task.CompletedTask;
    rect.LastGlobalScale = _globalScale;

    var scale = rect.OriginScale * rect.LastGlobalScale;
    rect.Scale = new Vector3(scale, scale, 1f);

    return Task.CompletedTask;
  }

  private async Task<Task> CheckAnchor(RectTransform rect) {
    // await Task.Delay(50);

    // var extent = _window.Extent;
    var extent = _application.Renderer.Extent2D;
    if (rect.LastScreenX == extent.Width && rect.LastScreenY == extent.Height && !rect.RequireUpdate) return Task.CompletedTask;

    rect.LastScreenX = extent.Width;
    rect.LastScreenY = extent.Height;
    // 100% = 1920x1080
    // 1% = axb
    // a = (1 * 1920) / 100
    // b = (1 * 1080) / 100

    var point = new Vector2(0, 0);
    var scaledOffset = rect.OffsetFromVector;
    scaledOffset = scaledOffset + (scaledOffset * rect.LastGlobalScale);

    switch (rect.Anchor) {
      case Anchor.Right:
        point.X = extent.Width - (scaledOffset.X);
        point.Y = extent.Height / 2;
        break;
      case Anchor.Left:
        point.X = 0 + (scaledOffset.X);
        point.Y = extent.Height / 2;
        break;
      case Anchor.Top:
        point.X = extent.Width / 2;
        point.Y = extent.Height + (scaledOffset.Y);
        break;
      case Anchor.Bottom:
        point.X = extent.Width / 2;
        point.Y = extent.Height - (scaledOffset.Y);
        break;
      case Anchor.RightTop:
        point.X = extent.Width - (scaledOffset.X);
        point.Y = 0 - (scaledOffset.Y);
        break;
      case Anchor.RightBottom:
        point.X = extent.Width - (scaledOffset.X);
        point.Y = extent.Height + (scaledOffset.Y);
        break;
      case Anchor.LeftTop:
        point.X = 0 + (scaledOffset.X);
        point.Y = 0 - (scaledOffset.Y);
        break;
      case Anchor.LeftBottom:
        point.X = 0 + (scaledOffset.X);
        point.Y = extent.Height + (scaledOffset.Y);
        break;
      case Anchor.Middle:
        point.X = extent.Width / 2 + scaledOffset.X;
        point.Y = extent.Height / 2 + scaledOffset.Y;
        break;
      case Anchor.MiddleBottom:
        point.X = extent.Width / 2 + scaledOffset.X;
        point.Y = extent.Height + scaledOffset.Y;
        break;
      case Anchor.MiddleTop:
        point.X = extent.Width / 2 + scaledOffset.X;
        point.Y = 0 + scaledOffset.Y;
        break;
      default:
        break;
    }

    var pos = Ray.ScreenPointToWorld2D(CameraState.GetCamera(), point, new Vector2(extent.Width, extent.Height));
    rect.Position.X = pos.X;
    rect.Position.Y = pos.Y;

    return Task.CompletedTask;
  }

  private Task CheckResolution() {
    if (_maxCanvasSize.X == _window.Extent.Width && _maxCanvasSize.Y == _window.Extent.Height) return Task.CompletedTask;

    _maxCanvasSize = new Vector2(_window.Extent.Width, _window.Extent.Height);
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
        _globalScale = 0.05f;
        break;
      case ResolutionSize.Screen1024x600:
        _globalScale = 0.1f;
        break;
      case ResolutionSize.Screen1334x750:
        _globalScale = 0.1f;
        break;
      case ResolutionSize.Screen1280x800:
        _globalScale = 0.1f;
        break;
      case ResolutionSize.Screen1600x900:
        _globalScale = 0.2f;
        break;
      case ResolutionSize.Screen1920x1080:
        _globalScale = 0.2f;
        break;
      case ResolutionSize.Screen2560x1080:
        _globalScale = 0.25f;
        break;
    }

    _currentResoltionScale = closestRes;
    return Task.CompletedTask;
  }

  public void Dispose() {
    foreach (var e in _entities) {
      e?.DisposeEverything();
    }
  }
}
