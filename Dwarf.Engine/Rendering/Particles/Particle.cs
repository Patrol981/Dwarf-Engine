using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;

namespace Dwarf.Rendering.Particles;

public class Particle {
  public const float GRAVITY = 9.807f;

  private readonly Application _app;

  private Vector3 _position;
  private Vector3 _velocity;
  private float _gravityEffect;
  private float _length;
  private float _rotation;
  private float _scale;
  private float _elapsed;

  public Particle(
    Application app,
    Vector3 position,
    Vector3 velocity,
    float gravityEffect,
    float length,
    float rotation,
    float scale
  ) {
    _position = position;
    _velocity = velocity;
    _gravityEffect = gravityEffect;
    _length = length;
    _rotation = rotation;
    _scale = scale;
  }

  public bool Update() {
    _velocity.Y -= GRAVITY * _gravityEffect * Time.DeltaTime;
    _position = Vector3.Add(_velocity * Time.DeltaTime, _position);
    _elapsed += Time.DeltaTime;

    return _elapsed < _length;
  }

  public void MarkToDispose() {
    CanBeDisposed = true;
  }

  public Vector3 Position => _position;
  public float Scale => _scale;
  public bool CanBeDisposed { get; private set; }
}