using System.Diagnostics;
using System.Numerics;

using JoltPhysicsSharp;

using static Dwarf.Engine.Physics.JoltConfig;

namespace Dwarf.Engine.Physics;

internal class BPLayerInterfaceImpl : BroadPhaseLayerInterface {
  private readonly BroadPhaseLayer[] _objectToBroadPhase = new BroadPhaseLayer[Layers.NumLayers];

  public BPLayerInterfaceImpl() {
    // Create a mapping table from object to broad phase layer
    _objectToBroadPhase[Layers.NonMoving] = BroadPhaseLayers.NonMoving;
    _objectToBroadPhase[Layers.Moving] = BroadPhaseLayers.Moving;
  }

  protected override int GetNumBroadPhaseLayers() {
    return BroadPhaseLayers.NumLayers;
  }

  protected override BroadPhaseLayer GetBroadPhaseLayer(ObjectLayer layer) {
    Debug.Assert(layer < Layers.NumLayers);
    return _objectToBroadPhase[layer];
  }

  protected override string GetBroadPhaseLayerName(BroadPhaseLayer layer) {
    switch ((byte)layer) {
      case BroadPhaseLayers.NonMoving: return "NON_MOVING";
      case BroadPhaseLayers.Moving: return "MOVING";
      default:
        Debug.Assert(false);
        return "INVALID";
    }
  }
}

internal class ObjectVsBroadPhaseLayerFilterImpl : ObjectVsBroadPhaseLayerFilter {
  protected override bool ShouldCollide(ObjectLayer layer1, BroadPhaseLayer layer2) {
    switch (layer1) {
      case Layers.NonMoving:
        return layer2 == BroadPhaseLayers.Moving;
      case Layers.Moving:
        return true;
      default:
        Debug.Assert(false);
        return false;
    }
  }
}

internal class ObjectLayerPairFilterImpl : ObjectLayerPairFilter {
  protected override bool ShouldCollide(ObjectLayer object1, ObjectLayer object2) {
    switch (object1) {
      case Layers.NonMoving:
        return object2 == Layers.Moving;
      case Layers.Moving:
        return true;
      default:
        Debug.Assert(false);
        return false;
    }
  }
}

public class JoltProgram {
  internal static Body CreateFloor(in BodyInterface bodyInterface, float size = 200.0f) {
    float scale = JoltConfig.WorldScale;

    Body floor = bodyInterface.CreateBody(new BodyCreationSettings(
        new BoxShapeSettings(scale * new Vector3(0.5f * size, 1.0f, 0.5f * size), 0.0f),
        scale * new Double3(0.0, 1.0, 0.0),
        Quaternion.Identity,
        MotionType.Static,
        JoltConfig.Layers.NonMoving)
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
      Matrix4x4 rotation = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, (float)torus_segment * 2.0f * (float)System.Math.PI / inTorusSegments);
      for (int tube_segment = 0; tube_segment < inTubeSegments; ++tube_segment) {
        // Create vertices
        float tube_angle = (float)tube_segment * 2.0f * (float)System.Math.PI / inTubeSegments;
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
    Console.WriteLine("Contact validate callback");

    // Allows you to ignore a contact before it is created (using layers to not make objects collide is cheaper!)
    return ValidateResult.AcceptAllContactsForThisBodyPair;
  }

  internal static void OnContactAdded(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2) {
    // Console.WriteLine("A contact was added");
  }

  internal static void OnContactPersisted(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2) {
    // Console.WriteLine("A contact was persisted");
  }

  internal static void OnContactRemoved(JoltPhysicsSharp.PhysicsSystem system, ref SubShapeIDPair subShapePair) {
    // Console.WriteLine("A contact was removed");
  }

  internal static void OnBodyActivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    // Console.WriteLine("A body got activated");
  }

  internal static void OnBodyDeactivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    // Console.WriteLine("A body went to sleep");
  }
}
