using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D.Interfaces;
using Vortice.Vulkan;

namespace Dwarf.Physics;

public class Rigidbody2D : Component, IDisposable {
  private readonly Application _app;
  private readonly VmaAllocator _vmaAllocator = VmaAllocator.Null;
  private IPhysicsBody2D _physicsBody2D = null!;
  private Mesh? _collisionShape;
  private Vector2 _min = Vector2.Zero;
  private Vector2 _max = Vector2.Zero;
  public MotionType MotionType { get; init; } = MotionType.Dynamic;
  public PrimitiveType PrimitiveType { get; init; } = PrimitiveType.None;

  public Vector2 Velocity => Vector2.Zero;
  public bool Kinematic => false;

  public Rigidbody2D() {
    _app = Application.Instance;
    _vmaAllocator = _app.VmaAllocator;
    PrimitiveType = PrimitiveType.Box;
  }

  public Rigidbody2D(
    Application app,
    PrimitiveType primitiveType
  ) {
    _app = app;
    _vmaAllocator = _app.VmaAllocator;
    PrimitiveType = primitiveType;
  }

  public Rigidbody2D(
    Application app,
    PrimitiveType primitiveType,
    Vector2 min,
    Vector2 max
  ) {
    _app = app;
    _vmaAllocator = _app.VmaAllocator;
    PrimitiveType = primitiveType;
    _min = min;
    _max = max;
  }

  public void Init(in IPhysicsBody2D physicsBody2D) {
    if (Owner.CanBeDisposed) throw new Exception("Entity is being disposed");
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_collisionShape == null) throw new ArgumentNullException(nameof(_collisionShape));
    if (_app.Device == null) throw new Exception("Device cannot be null!");

    _physicsBody2D = physicsBody2D;

    var pos = Owner.GetComponent<Transform>().Position;
    object shapeSettings = null!;
    _physicsBody2D.CreateAndAddBody(MotionType, shapeSettings, pos.ToVector2());
    _physicsBody2D.GravityFactor = 0.1f;
  }

  public void InitBase() {
    _collisionShape = PrimitiveType switch {
      PrimitiveType.Convex => ((IDrawable2D)Owner.GetDrawable<IDrawable2D>()).CollisionMesh.Clone() as Mesh,
      PrimitiveType.Box => Primitives2D.CreateQuad2D(_min, _max),
      _ => throw new NotImplementedException(),
    };

    Owner.AddComponent(new ColliderMesh(_app.VmaAllocator, _app.Device, _collisionShape!));
  }

  public void Update() {
    if (Owner.CanBeDisposed) return;

    var pos = _physicsBody2D?.Position;
    var transform = Owner!.GetComponent<Transform>();

    transform.Position.X = pos.HasValue ? pos.Value.X : 0;
    transform.Position.Y = pos.HasValue ? pos.Value.Y : 0;
  }

  public void AddForce() {

  }

  public void AddVelocity() {

  }

  public void AddImpule() {

  }

  public void Translate() {

  }

  public void SetPosition() {

  }

  public void InvokeCollision(CollisionState collisionState, Entity otherColl) {
    if (Owner.CanBeDisposed) return;
    var scripts = Owner!.GetScripts();
    for (short i = 0; i < scripts.Length; i++) {
      switch (collisionState) {
        case CollisionState.Enter:
          scripts[i].CollisionEnter(otherColl);
          break;
        case CollisionState.Stay:
          scripts[i].CollisionStay(otherColl);
          break;
        case CollisionState.Exit:
          scripts[i].CollisionExit(otherColl);
          break;
        default:
          break;
      }
    }
  }

  public void Dispose() {
    GC.SuppressFinalize(this);
  }
}