using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Hammer.Models;
using Dwarf.Math;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D.Components;
using Dwarf.Rendering.Renderer2D.Helpers;
using Dwarf.Rendering.Renderer2D.Interfaces;
using Vortice.Vulkan;

namespace Dwarf.Physics;

public class Rigidbody2D : Component, IDisposable {
  private readonly Application _app;
  private readonly VmaAllocator _vmaAllocator = VmaAllocator.Null;
  public IPhysicsBody2D PhysicsBody2D { get; private set; } = null!;
  private Mesh? _collisionShape;
  public Vector2 Min { get; private set; } = Vector2.Zero;
  public Vector2 Max { get; private set; } = Vector2.Zero;
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
    PrimitiveType primitiveType,
    MotionType motionType
  ) {
    _app = app;
    _vmaAllocator = _app.VmaAllocator;
    MotionType = motionType;
    PrimitiveType = primitiveType;
  }

  public Rigidbody2D(
    Application app,
    PrimitiveType primitiveType,
    MotionType motionType,
    Vector2 min,
    Vector2 max
  ) {
    _app = app;
    _vmaAllocator = _app.VmaAllocator;
    PrimitiveType = primitiveType;
    MotionType = motionType;
    Min = min;
    Max = max;
  }

  public void Init(in IPhysicsBody2D physicsBody2D) {
    if (Owner.CanBeDisposed) throw new Exception("Entity is being disposed");
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_collisionShape == null) throw new ArgumentNullException(nameof(_collisionShape));
    if (_app.Device == null) throw new Exception("Device cannot be null!");

    PhysicsBody2D = physicsBody2D;

    var pos = Owner.GetComponent<Transform>().Position;
    var shapeSettings = PhysicsBody2D.ColldierMeshToPhysicsShape(Owner, _collisionShape);
    PhysicsBody2D.CreateAndAddBody(MotionType, shapeSettings, pos.ToVector2());
    PhysicsBody2D.GravityFactor = 0.1f;
  }

  public void InitBase() {
    var scale = Owner.GetComponent<Transform>().Scale;
    Min = new Vector2(Min.X * scale.X, Min.Y * scale.Y);
    Max = new Vector2(Max.X * scale.X, Max.Y * scale.Y);

    _collisionShape = PrimitiveType switch {
      PrimitiveType.Convex => GetFromOwner(),
      PrimitiveType.Box => Primitives2D.CreateQuad2D(Min, Max),
      _ => throw new NotImplementedException(),
    };

    Owner.AddComponent(new ColliderMesh(_app.VmaAllocator, _app.Device, _collisionShape!));
  }

  private Mesh GetFromOwner() {
    var mesh = ((IDrawable2D)Owner.GetDrawable<IDrawable2D>()).CollisionMesh.Clone() as Mesh;
    var scale = Owner.GetComponent<Transform>().Scale;
    for (int i = 0; i < mesh!.Vertices.Length; i++) {
      mesh.Vertices[i].Position.X *= scale.X;
      mesh.Vertices[i].Position.Y *= scale.Y;
      mesh.Vertices[i].Position.Z *= scale.Z;
    }

    return mesh;
  }

  public void Update() {
    if (Owner.CanBeDisposed) return;

    var pos = PhysicsBody2D?.Position;
    var transform = Owner!.GetComponent<Transform>();

    transform.Position.X = pos.HasValue ? pos.Value.X : 0;
    transform.Position.Y = pos.HasValue ? pos.Value.Y : 0;
  }

  public void AddForce(Vector2 vec2) {
    if (Owner.CanBeDisposed) return;
    PhysicsBody2D.AddForce(vec2);
  }

  public void AddVelocity(Vector2 vec2) {
    if (Owner.CanBeDisposed) return;
    PhysicsBody2D.AddLinearVelocity(vec2);
  }

  public void AddImpule(Vector2 vec2) {
    if (Owner.CanBeDisposed) return;
    PhysicsBody2D.AddImpulse(vec2);
  }

  public void Translate(Vector2 vec2) {
    if (Owner.CanBeDisposed) return;
    PhysicsBody2D.AddLinearVelocity(vec2);
  }

  public void SetPosition(Vector2 vec2) {
    if (Owner.CanBeDisposed) return;
    PhysicsBody2D.Position = vec2;
  }

  public void InvokeCollision(CollisionState collisionState, Entity? otherColl) {
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
    PhysicsBody2D.Dispose();
    GC.SuppressFinalize(this);
  }
}