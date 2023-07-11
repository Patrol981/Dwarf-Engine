using System.Numerics;

using JoltPhysicsSharp;

namespace Dwarf.Engine.Physics;
public class JoltProgram {
  private static Body CreateFloor(in BodyInterface bodyInterface, float size = 200.0f) {
    float scale = JoltConfig.WorldScale;

    Body floor = bodyInterface.CreateBody(new BodyCreationSettings(
        new BoxShapeSettings(scale * new Vector3(0.5f * size, 1.0f, 0.5f * size), 0.0f),
        scale * new Double3(0.0, -1.0, 0.0),
        Quaternion.Identity,
        MotionType.Static,
        JoltConfig.Layers.NonMoving)
        );
    bodyInterface.AddBody(floor.ID, Activation.DontActivate);
    return floor;
  }

  private static ValidateResult OnContactValidate(PhysicsSystem system, in Body body1, in Body body2, Double3 baseOffset, IntPtr collisionResult) {
    Console.WriteLine("Contact validate callback");

    // Allows you to ignore a contact before it is created (using layers to not make objects collide is cheaper!)
    return ValidateResult.AcceptAllContactsForThisBodyPair;
  }

  private static void OnContactAdded(PhysicsSystem system, in Body body1, in Body body2) {
    Console.WriteLine("A contact was added");
  }

  private static void OnContactPersisted(PhysicsSystem system, in Body body1, in Body body2) {
    Console.WriteLine("A contact was persisted");
  }

  private static void OnContactRemoved(PhysicsSystem system, ref SubShapeIDPair subShapePair) {
    Console.WriteLine("A contact was removed");
  }

  private static void OnBodyActivated(PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    Console.WriteLine("A body got activated");
  }

  private static void OnBodyDeactivated(PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    Console.WriteLine("A body went to sleep");
  }
}
