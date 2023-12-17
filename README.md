# Dwarf Engine

Hello, my name is Patrick and I'm author of this game engine Dwarf Engine is my
approach for game engines topic.

The engine itself is not meant by any means for enterprice general use cases; it
is strictly designed to match my expectations of an game engine so I can create
some cool games with it :)

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
using Dwarf.Engine.Global;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Rendering;

namespace TanksGame;
public class App {
	private readonly Application _application;
	private float _time = 0.0f;
	private double _frames = 0.0f;

	public  App() {
		var  systems = SystemCreationFlags.Renderer3D |
					   SystemCreationFlags.Physics3D  |
					   SystemCreationFlags.RendererUI;

		_application = new Application("Tanks!", systems);
		var  scene = new DebugScene(_application);
		_application.SetupScene(scene);
		_application.SetUpdateCallback(Update);
		_application.Init();
		_application.Run();
	}

	public  void  Update() {
		_frames = Frames.GetFrames();
		_time += Time.DeltaTime;
		if (_time > 2) {
			_time = 0;
			_application.Window.SetWindowName($"Tanks! - {_frames} FPS");
		}
	}
}
```

```csharp
//DebugScene.cs

using System.Numerics;
using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Rendering.UI;

namespace TanksGame;

public class DebugScene : Scene {
	public DebugScene(Application  app) : base(app) { }

	public async override void LoadEntities() {
		base.LoadEntities();

		var canvas = new Entity();
		canvas.AddComponent(new Canvas());
		canvas.GetComponent<Canvas>().CreateText("HP:0", Anchor.Bottom, new(0, 100), "hpInfo", 2);
		AddEntity(canvas);

		// One way of creating game object
		var tank = await EntityCreator.Create3DModel(
		"player",
		"./Resources/tank.glb",
		null!,
		new(0, 0, 0),
		new(180, 0, 0),
		new(1, 1, 1),
		false,
		0
		);
		EntityCreator.AddRigdbody(_app.Device, ref tank,PrimitiveType.Convex, 1, false);
		tank.Name = "tank1";
		tank.AddComponent(new TankController());
		AddEntity(tank);

		// Alternative option for creating game object, using builder pattern
		var otherTank = new Entity();
		otherTank.Name = "tank2";
		otherTank.AddTransform(new(1, 0, 3), new(180, 0, 0), Vector3.One);
		otherTank.AddMaterial();
		otherTank.AddModel("./Resources/tank.glb", 0);
		otherTank.AddRigdbody(PrimitiveType.Convex, false, 0.4f);
		otherTank.AddComponent(new TankData());
		AddEntity(otherTank);

		var camera = new Entity();
		camera.AddComponent(new Transform(new  Vector3(0, -10, 0)));
		camera.AddComponent(new Camera(50, _app.Renderer.AspectRatio));
		camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.01f, 100f);
		camera.GetComponent<Camera>().Pitch = 90;
		CameraState.SetCamera(camera.GetComponent<Camera>());
		CameraState.SetCameraEntity(camera);
		CameraState.SetCameraSpeed(CameraState.GetCameraSpeed() *  2);
		_app.SetCamera(camera);
	}


	public override void LoadTextures() {
		base.LoadTextures();
		string[] paths  = {
			$"./Fonts/atlas.png",
			$"./Resources/gigachad.png"
		};
		List<List<string>> loaderPaths  =  new() { paths.ToList() };
		SetTexturePaths(loaderPaths);
	}
}
```

```csharp
// TankController

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Physics;
using Dwarf.Extensions.Logging;
using System.Numerics;

namespace TanksGame;
public class TankController : DwarfScript {

	private Rigidbody? _rigidbody;
	private Transform? _transform;

	public override void Start() {
		base.Start();
		_rigidbody = Owner!.GetComponent<Rigidbody>();
		_transform = Owner!.GetComponent<Transform>();
	}

	public override void Update() {
		base.Update();
		if (Input.GetKey(Dwarf.Keys.GLFW_KEY_UP)) {
			_rigidbody?.Translate(-_transform!.Forward * Time.DeltaTime);
		}

		if (Input.GetKey(Dwarf.Keys.GLFW_KEY_DOWN)) {
			_rigidbody?.Translate(_transform!.Forward * Time.DeltaTime);
		}

		if (Input.GetKey(Dwarf.Keys.GLFW_KEY_LEFT)) {
			var rotation = new Vector3(0, 1, 0) * 70 * Time.DeltaTime;
			_transform?.IncreaseRotation(rotation);
		}

		if (Input.GetKey(Dwarf.Keys.GLFW_KEY_RIGHT)) {
			var rotation = new Vector3(0, -1, 0) * 70 * Time.DeltaTime;
			_transform?.IncreaseRotation(rotation);
		}
	}
}
```
