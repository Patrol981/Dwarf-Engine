using System.Numerics;

using JoltPhysicsSharp;

using static Dwarf.Physics.JoltConfig;

namespace Dwarf.Physics;

public static class JoltProgram {
  internal static Body CreateFloor(in BodyInterface bodyInterface, float size = 200.0f) {
    float scale = WorldScale;

    Body floor = bodyInterface.CreateBody(new BodyCreationSettings(
        new BoxShapeSettings(scale * new Vector3(0.5f * size, 1.0f, 0.5f * size), 0.0f),
        scale * new Double3(0.0, 1.0, 0.0),
        Quaternion.Identity,
        MotionType.Static,
        Layers.NonMoving)
        );
    bodyInterface.AddBody(floor.ID, Activation.DontActivate);
    return floor;
  }

  public static ValidateResult OnContactValidate(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, Double3 baseOffset, IntPtr collisionResult) {
    // Allows you to ignore a contact before it is created (using layers to not make objects collide is cheaper!)
    return ValidateResult.AcceptAllContactsForThisBodyPair;
  }

  internal static void OnContactAdded(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, in ContactManifold manifold, in ContactSettings settings) {
    var data = Rigidbody.GetCollisionData(body1.ID, body2.ID);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Enter, data.Item2);
      data.Item2.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Enter, data.Item1);
    }
  }

  internal static void OnContactPersisted(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, in ContactManifold manifold, in ContactSettings settings) {
    var data = Rigidbody.GetCollisionData(body1.ID, body2.ID);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Stay, data.Item2);
      data.Item2.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Stay, data.Item1);
    }
  }

  internal static void OnContactRemoved(JoltPhysicsSharp.PhysicsSystem system, ref SubShapeIDPair subShapePair) {
    var data = Rigidbody.GetCollisionData(subShapePair.Body1ID, subShapePair.Body2ID);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Exit, data.Item2);
      data.Item2.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Exit, data.Item1);
    }
  }

  internal static void OnBodyActivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    // Console.WriteLine("A body got activated");
  }

  internal static void OnBodyDeactivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    // Console.WriteLine("A body went to sleep");
  }
}
