

# Dwarf Engine
Hello, my name is Patrick and I'm author of this game engine
Dwarf Engine is my approach for game engines topic.

The engine itself is not meant by any means for enterprice general use cases; it is strictly
designed to match my expectations of an game engine so I can create some cool games with it :)

## Features
- 2D
- 3D
- Block-styled systems
- Cross-platform
- Entity Component System

## How to use it
Project itself is a library, so you can reference it in your .csproj

## Example code
```csharp
//Program.cs
using Dwarf.Client;
var main = new App();
```
```csharp
//App.cs
using Dwarf.Engine;
using Dwarf.Engine.Rendering;

using Dwarf.Client.Scripts;
using Dwarf.Extensions.Logging;
using static Dwarf.Extensions.GLFW.GLFW;
using Dwarf.Engine.Global;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Rendering.UI;
using DwarfClient;
using DwarfClient.Scripts;

namespace Dwarf.Client;
public class App {
  private readonly Application _application;

  private float _time = 0.0f;
  private int _displayNumber = 0;
  private double _frames = 0.0f;

  private Canvas _canvas = null!;

  public App() {
    var appName = "Dwarf Client";
    var systems = SystemCreationFlags.Renderer3D |
                  SystemCreationFlags.RendererUI |
                  SystemCreationFlags.Physics3D;
    _application = new Application(appName, systems);
    var scene = new RPGScene(_application);
    _application.SetupScene(scene);
    _application.Init();
    _application.SetUpdateCallback(Update3D);
    _application.SetRenderCallback(Render);
    _application.SetOnLoadCallback(Load);

    var cnvEntity = _application.GetEntities().Where(x => x.HasComponent<Canvas>()).FirstOrDefault();
    if (cnvEntity == null) {
      Logger.Warn("[CANVAS] No canvas provided!");
      return;
    } else {
      _canvas = cnvEntity!.GetComponent<Canvas>();
    }

    _application.Run();
  }

  private void Load() {
    // Additional load method is being invoked before starting main engine loop
  }

  private void Update3D() {
    var entities = _application.GetEntities();

    _frames = Frames.GetFrames();

    _time += Time.DeltaTime;
    if (_time > 2) {
      _time = 0;
      _displayNumber++;
      _application.Window.SetWindowName($"Dwarf Client - {_frames} FPS");
    }

    _canvas?.Update();

    foreach (var entity in entities) {
      entity.GetComponent<TransformController>()?.Update();
      entity.GetComponent<MouseController>()?.Update();
    }
  }

  private void Render() {
    // Render method is being invoked during engine render loop
  }
}
```
```csharp
//RPGScene.cs

using Dwarf.Client.Scripts;
using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Physics;
using Dwarf.Engine.Procedural;
using Dwarf.Engine.Rendering.UI;

using System.Numerics;

namespace DwarfClient;
public class RPGScene : Scene {
  public const string TEX_PREFIX = "./Resources/Textures/";
  public const string MODELS_PREFIX = "./Resources/Models/";

  public RPGScene(Application app) : base(app) { }

  public async override void LoadEntities() {
    base.LoadEntities();

    var canvas = new Entity();
    canvas.AddComponent(new Canvas());
    AddEntity(canvas);

    canvas.GetComponent<Canvas>().CreateImage(
      "./Resources/gigachad.png",
      Anchor.RightTop,
      new(100, -100),
      "gigachadImage3"
    );

    var player = await EntityCreator.Create3DModel(
      "Player",
      $"{MODELS_PREFIX}chr_sword.obj",
      new string[] { $"{TEX_PREFIX}chr_sword/chr_sword.png" },
      null,
      new(180, 0, 0)
    );
    EntityCreator.AddRigdbody(_app.Device, ref player, PrimitiveType.Cylinder, 0.25f);
    player.AddComponent(new TransformController());
    AddEntity(player);

    var npc = await EntityCreator.Create3DModel(
      "NPC",
      $"{MODELS_PREFIX}chr_knight.obj",
      new string[] { $"{TEX_PREFIX}chr_knight/chr_knight.png" },
      new(15, -5, 0),
      new(180, 0, 0)
    );
    EntityCreator.AddRigdbody(_app.Device, ref npc, PrimitiveType.Box, 0.25f);
    AddEntity(npc);

    var house = await EntityCreator.Create3DModel(
      "viking house",
      $"{MODELS_PREFIX}viking_room.obj",
      new string[] { $"{TEX_PREFIX}viking_room/viking_room.png" },
      new(0, 0.25f, -6),
      new(90, 0, 0),
      new(5, 5, 5)
    );
    EntityCreator.AddRigdbody(_app.Device, ref house, PrimitiveType.Cylinder, 0.9f);
    AddEntity(house);

    var terrain = await EntityCreator.CreateBase("terrain3D", new(-256, 2, -256), new(0, 0, 0), new(1, 1, 1));
    terrain.AddComponent(new Terrain3D(_app));
    terrain.GetComponent<Terrain3D>().Setup();
    EntityCreator.AddRigdbody(_app.Device, ref terrain, PrimitiveType.Convex, 0);
    terrain.GetComponent<Rigidbody>().Kinematic = true;
    AddEntity(terrain);


    var camera = new Entity();
    camera.AddComponent(new Transform(new Vector3(0, -3, 0)));
    camera.AddComponent(new Camera(60, _app.Renderer.AspectRatio));
    camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
    camera.GetComponent<Camera>().Yaw = 1.3811687f;
    // camera.AddComponent(new FreeCameraController());
    camera.AddComponent(new ThirdPersonCamera());
    camera.GetComponent<ThirdPersonCamera>().Init(player);
    CameraState.SetCamera(camera.GetComponent<Camera>());
    CameraState.SetCameraEntity(camera);
    CameraState.SetCameraSpeed(CameraState.GetCameraSpeed() * 2);
    _app.SetCamera(camera);
  }

  public override void LoadTextures() {
    base.LoadTextures();

    string[] paths = {
      "./Fonts/atlas.png",
      $"{TEX_PREFIX}viking_room/viking_room.png",
      $"{TEX_PREFIX}chr_knight/chr_knight.png",
      $"{TEX_PREFIX}chr_sword/chr_sword.png",
      $"./Resources/gigachad.png"
    };

    List<List<string>> loaderPaths = new() { paths.ToList() };
    SetTexturePaths(loaderPaths);
  }
}
```