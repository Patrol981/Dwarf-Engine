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
    if (!Foundation.Init(false)) {
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

    // Optional step: Before starting the physics simulation you can optimize the broad phase. This improves collision detection performance (it's pointless here because we only have 2 bodies).
    // You should definitely not call this every frame or when e.g. streaming in a new level section as it is an expensive operation.
    // Instead insert all new objects in batches instead of 1 at a time to keep the broad phase efficient.
    // _physicsSystem.OptimizeBroadPhase();
    _physicsSystem.Gravity *= -1;
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

    _physicsSystem.Step(Time.FixedTime * 10, CollisionSteps);
    return Task.CompletedTask;
  }

  public void Dispose() {
    _physicsSystem?.Dispose();
    Foundation.Shutdown();
  }

  public BodyInterface BodyInterface => _physicsSystem.BodyInterface;
}
