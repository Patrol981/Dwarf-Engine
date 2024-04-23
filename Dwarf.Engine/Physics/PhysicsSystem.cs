using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;

using JoltPhysicsSharp;

using static Dwarf.Physics.JoltConfig;

namespace Dwarf.Physics;

public delegate void PhysicsSystemCallback();

public class PhysicsSystem : IDisposable {
  // We simulate the physics world in discrete time steps. 60 Hz is a good rate to update the physics system.
  public const float DeltaTime = 1.0f / 600.0f;
  public const int CollisionSteps = 1;

  private readonly JoltPhysicsSharp.PhysicsSystem _physicsSystem = null!;
  public PhysicsSystem() {
    if (!Foundation.Init(0u, false)) {
      return;
    }

    Logger.Info($"[CREATING PHYSICS 3D]");

    // We use only 2 layers: one for non-moving objects and one for moving objects
    var objectLayerPairFilter = new ObjectLayerPairFilterTable(2);
    objectLayerPairFilter.EnableCollision(Layers.NonMoving, Layers.Moving);
    objectLayerPairFilter.EnableCollision(Layers.Moving, Layers.Moving);

    // We use a 1-to-1 mapping between object layers and broadphase layers
    var broadPhaseLayerInterface = new BroadPhaseLayerInterfaceTable(2, 2);
    broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.NonMoving, BroadPhaseLayers.NonMoving);
    broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.Moving, BroadPhaseLayers.Moving);

    var objectVsBroadPhaseLayerFilter = new ObjectVsBroadPhaseLayerFilterTable(broadPhaseLayerInterface, 2, objectLayerPairFilter, 2);

    PhysicsSystemSettings settings = new() {
      ObjectLayerPairFilter = objectLayerPairFilter,
      BroadPhaseLayerInterface = broadPhaseLayerInterface,
      ObjectVsBroadPhaseLayerFilter = objectVsBroadPhaseLayerFilter
    };

    _physicsSystem = new(settings);

    // ContactListener
    _physicsSystem.OnContactValidate += JoltProgram.OnContactValidate;
    _physicsSystem.OnContactAdded += JoltProgram.OnContactAdded;
    _physicsSystem.OnContactPersisted += JoltProgram.OnContactPersisted;
    _physicsSystem.OnContactRemoved += JoltProgram.OnContactRemoved;
    // BodyActivationListener
    _physicsSystem.OnBodyActivated += JoltProgram.OnBodyActivated;
    _physicsSystem.OnBodyDeactivated += JoltProgram.OnBodyDeactivated;

    var bodyInterface = _physicsSystem.BodyInterface;

    // Next we can create a rigid body to serve as the floor, we make a large box
    // Create the settings for the collision volume (the shape).
    // Note that for simple shapes (like boxes) you can also directly construct a BoxShape.
    BoxShapeSettings floorShapeSettings = new(new System.Numerics.Vector3(100.0f, 1.0f, 100.0f));
    BodyCreationSettings floorSettings = new(floorShapeSettings, new Double3(0.0f, 5.0f, 0.0f), Quaternion.Identity, MotionType.Static, Layers.NonMoving);

    var floor = bodyInterface.CreateBody(floorSettings);
    bodyInterface.AddBody(floor, Activation.DontActivate);

    // BodyCreationSettings sphereSettings = new(new SphereShape(0.5f), new Double3(0.0f, 2.0f, 0.0f), Translator.OpenTKToSystemNumericsQuaternion(Quaternion.Identity), MotionType.Dynamic, Layers.Moving);
    // BodyID sphereID = bodyInterface.CreateAndAddBody(sphereSettings, Activation.Activate);

    // Now you can interact with the dynamic body, in this case we're going to give it a velocity.
    // (note that if we had used CreateBody then we could have set the velocity straight on the body before adding it to the physics system)
    // var vec3 = new Vector3(0.0f, -5.0f, 0.0f);
    // bodyInterface.SetLinearVelocity(sphereID, Translator.OpenTKToSystemNumericsVector(vec3));
    // JoltProgram.StackTest(bodyInterface);

    // MeshShapeSettings meshShape = JoltProgram.CreateTorusMesh(3.0f, 1.0f);
    // BodyCreationSettings settings = new(meshShape, new Double3(0, 10, 0), OpenTKToSystemNumericsQuaternion(Quaternion.Identity), MotionType.Dynamic, Layers.Moving);



    // Optional step: Before starting the physics simulation you can optimize the broad phase. This improves collision detection performance (it's pointless here because we only have 2 bodies).
    // You should definitely not call this every frame or when e.g. streaming in a new level section as it is an expensive operation.
    // Instead insert all new objects in batches instead of 1 at a time to keep the broad phase efficient.
    // _physicsSystem.OptimizeBroadPhase();
    _physicsSystem.Gravity *= -1;
    // _physicsSystem.BodyInterface.SetGravityFactor(0.01);
    // _physicsSystem.Gravity /= 25;
    Logger.Info($"[GRAVITY] {_physicsSystem.Gravity}");
  }

  public void Init(Span<Entity> entities) {
    foreach (var entity in entities) {
      entity.GetComponent<Rigidbody>()?.Init(BodyInterface);
    }
  }

  public Task Tick(Entity[] entities) {
    for (short i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      entities[i].GetComponent<Rigidbody>()?.Update();
    }

    // _physicsSystem.OptimizeBroadPhase();
    _physicsSystem.Step(Time.DeltaTime * 10, CollisionSteps);
    return Task.CompletedTask;

    // Console.WriteLine(entities.Length);
    // Logger.Info("Physics Tick");
    // return Task.CompletedTask;
  }

  public static void Calculate(object application) {
    var app = (Application)application;
    var systems = app.GetSystems();
    var entities = Entity.Distinct<Rigidbody>(app.GetEntities());
    // var system = (PhysicsSystem)physicsSystem;

    while (!app.Window.ShouldClose) {
      // systems.PhysicsSystem.Tick(entities);
      // Thread.Sleep(50);
    }
  }

  public void Dispose() {
    _physicsSystem?.Dispose();

    Foundation.Shutdown();
  }

  public BodyInterface BodyInterface => _physicsSystem.BodyInterface;
}
