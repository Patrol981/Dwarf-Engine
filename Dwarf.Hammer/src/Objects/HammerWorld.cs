using System.Numerics;
using Dwarf.Hammer.Colliders;

namespace Dwarf.Hammer;

public class HammerWorld {
  private List<HammerObject> _hammerObjects = [];
  private List<HammerSolver> _hammerSolvers = [];
  private float _gravity = 9.807f;

  public void AddObject(HammerObject hammerObject) {
    _hammerObjects.Add(hammerObject);
  }

  public void RemoveObject(HammerObject hammerObject) {
    if (_hammerObjects.Contains(hammerObject)) {
      _hammerObjects.Remove(hammerObject);
    }
  }

  public void AddSolver(HammerSolver hammerSolver) {
    _hammerSolvers.Add(hammerSolver);
  }

  public void RemoveSolver(HammerSolver hammerSolver) {
    if (_hammerSolvers.Contains(hammerSolver)) {
      _hammerSolvers.Remove(hammerSolver);
    }
  }

  public void Step(float timeDelta) {
    ResolveCollisions(timeDelta);

    foreach (var obj in _hammerObjects) {
      // var result = obj.Mass * _gravity;
      // obj.Force.X += result;
      // obj.Force.Y += result;
      // obj.Force.Z += result;

      // obj.Velocity += obj.Force / obj.Mass * timeDelta;
      // obj.Position += obj.Velocity * timeDelta;

      // obj.Force = Vector3.Zero;
    }
  }

  private void ResolveCollisions(float timeDelta) {
    var collsions = new List<Collision>();
    foreach (var objA in _hammerObjects) {
      foreach (var objB in _hammerObjects) {
        if (objA == objB) break;
        if (objA.Collider == null || objB.Collider == null) continue;

        var points = objA.Collider.TestCollision(objA.Transform, objB.Collider, objB.Transform);

        if (points.HasCollision) {
          collsions.Insert(0, new(objA, objB, points));
        }
      }
    }

    foreach (var solver in _hammerSolvers) {
      solver.Solve(collsions, timeDelta);
    }
  }
}