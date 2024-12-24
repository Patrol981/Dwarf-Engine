using System.Numerics;

namespace Dwarf.Rendering.Particles;

public class ParticleBatch {
  private List<Particle> _particles = [];

  public ParticleBatch() {

  }

  protected ParticleBatch(List<Particle> particles) {
    _particles = particles;
  }

  internal List<Particle> Particles => _particles;

  public class Builder(Application app) {
    private readonly Application _app = app;
    private List<Particle> _particles = [];

    public Builder AddParticle(
      Vector3 position,
      Vector3 velocity,
      float gravityEffect,
      float length,
      float rotation,
      float scale
    ) {
      _particles.Add(new Particle(
        _app,
        position,
        velocity,
        gravityEffect,
        length,
        rotation,
        scale
      ));

      return this;
    }

    public Builder PropagateParticles(int count) {
      var rnd = new Random();

      for (int i = 0; i < count; i++) {

      }

      return this;
    }

    public ParticleBatch Build() {
      return new ParticleBatch(_particles);
    }
  }
}