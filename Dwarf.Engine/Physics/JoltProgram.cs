using System.Diagnostics;
using System.Numerics;

using JoltPhysicsSharp;

using static Dwarf.Engine.Physics.JoltConfig;

namespace Dwarf.Engine.Physics;

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

  internal static MeshShapeSettings CreateTorusMesh(float inTorusRadius, float inTubeRadius, int inTorusSegments = 16, int inTubeSegments = 16) {
    int cNumVertices = inTorusSegments * inTubeSegments;

    // Create torus
    int triangleIndex = 0;
    Span<Vector3> triangleVertices = stackalloc Vector3[cNumVertices];
    Span<IndexedTriangle> indexedTriangles = stackalloc IndexedTriangle[cNumVertices * 2];

    for (int torus_segment = 0; torus_segment < inTorusSegments; ++torus_segment) {
      Matrix4x4 rotation = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, torus_segment * 2.0f * (float)System.Math.PI / inTorusSegments);
      for (int tube_segment = 0; tube_segment < inTubeSegments; ++tube_segment) {
        // Create vertices
        float tube_angle = tube_segment * 2.0f * (float)System.Math.PI / inTubeSegments;
        Vector3 pos = Vector3.Transform(
            new Vector3(inTorusRadius + inTubeRadius * (float)System.Math.Sin(tube_angle), inTubeRadius * (float)System.Math.Cos(tube_angle), 0),
            rotation);
        triangleVertices[triangleIndex] = pos;

        // Create indices
        int start_idx = torus_segment * inTubeSegments + tube_segment;
        indexedTriangles[triangleIndex] = new(start_idx, (start_idx + 1) % cNumVertices, (start_idx + inTubeSegments) % cNumVertices);
        indexedTriangles[triangleIndex + 1] = new((start_idx + 1) % cNumVertices, (start_idx + inTubeSegments + 1) % cNumVertices, (start_idx + inTubeSegments) % cNumVertices);

        triangleIndex++;
      }
    }

    return new MeshShapeSettings(triangleVertices, indexedTriangles);
  }

  internal static void StackTest(in BodyInterface bodyInterface) {
    // Floor
    CreateFloor(bodyInterface);

    Shape boxShape = new BoxShape(new Vector3(0.5f, 1.0f, 2.0f));

    // Dynamic body stack
    for (int i = 0; i < 10; ++i) {
      Quaternion rotation;
      if ((i & 1) != 0)
        rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f * (float)System.Math.PI);
      else
        rotation = Quaternion.Identity;
      Body stack = bodyInterface.CreateBody(new BodyCreationSettings(boxShape, new Vector3(10, 1.0f + i * 2.1f, 0), rotation, MotionType.Dynamic, Layers.Moving));
      bodyInterface.AddBody(stack.ID, Activation.Activate);
    }
  }

  public static ValidateResult OnContactValidate(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, Double3 baseOffset, IntPtr collisionResult) {
    // Allows you to ignore a contact before it is created (using layers to not make objects collide is cheaper!)
    return ValidateResult.AcceptAllContactsForThisBodyPair;
  }

  internal static void OnContactAdded(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2) {
    var data = Rigidbody.GetCollisionData(body1.ID, body2.ID);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Enter, data.Item2);
      data.Item2.GetComponent<Rigidbody>().InvokeCollision(CollisionState.Enter, data.Item1);
    }
  }

  internal static void OnContactPersisted(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2) {
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
