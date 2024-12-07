using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Rendering;

namespace Dwarf.Math;

public static class Frustum {
  public static List<T> FilterObjectsByPlanes<T>(in Plane[] planes, Span<T> objects) where T : IRender3DElement {
    var filteredObjects = new List<T>();

    foreach (var obj in objects) {
      // var aabb = obj.GetOwner().GetComponent<MeshRenderer>().AABB;
      // if (IsInAABBFrustum(
      //     planes,
      //     aabb.Min,
      //     aabb.Max
      //   )
      // ) {
      //   filteredObjects.Add(obj);
      // }
      if (IsInSphereFrustum(
        planes,
        obj.GetOwner().GetComponent<Transform>().Position,
        obj.GetOwner().GetComponent<MeshRenderer>().Radius
        )
      ) {
        filteredObjects.Add(obj);
      }
    }

    return filteredObjects;
  }
  public static void GetFrustrum(out Plane[] planes) {
    var camera = CameraState.GetCamera();
    var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();

    planes = new Plane[6];

    // Left Plane
    planes[0] = new Plane(
        new Vector3(
            viewProjection.M14 + viewProjection.M11,
            viewProjection.M24 + viewProjection.M21,
            viewProjection.M34 + viewProjection.M31
        ),
        viewProjection.M44 + viewProjection.M41
    );

    // Right Plane
    planes[1] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M11,
            viewProjection.M24 - viewProjection.M21,
            viewProjection.M34 - viewProjection.M31
        ),
        viewProjection.M44 - viewProjection.M41
    );

    // Bottom Plane
    planes[2] = new Plane(
        new Vector3(
            viewProjection.M14 + viewProjection.M12,
            viewProjection.M24 + viewProjection.M22,
            viewProjection.M34 + viewProjection.M32
        ),
        viewProjection.M44 + viewProjection.M42
    );

    // Top Plane
    planes[3] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M12,
            viewProjection.M24 - viewProjection.M22,
            viewProjection.M34 - viewProjection.M32
        ),
        viewProjection.M44 - viewProjection.M42
    );

    // Near Plane
    planes[4] = new Plane(
        new Vector3(
          viewProjection.M14 + viewProjection.M13,
          viewProjection.M24 + viewProjection.M23,
          viewProjection.M34 + viewProjection.M33
        ),
        viewProjection.M43 + viewProjection.M43
    );

    // Far Plane
    planes[5] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M13,
            viewProjection.M24 - viewProjection.M23,
            viewProjection.M34 - viewProjection.M33
        ),
        viewProjection.M44 - viewProjection.M43
    );

    // Normalize planes
    for (int i = 0; i < 6; i++) {
      planes[i] = NormalizePlane(planes[i]);
    }
  }

  private static Plane NormalizePlane(Plane plane) {
    float magnitude = plane.Normal.Length();
    return new Plane(plane.Normal / magnitude, plane.D / magnitude);
  }

  public static bool IsInSphereFrustum(in Plane[] planes, Vector3 center, float radius) {
    for (int i = 0; i < planes.Length; i++) {
      float distance = Vector3.Dot(planes[i].Normal, center) + planes[i].D;
      if (distance < -radius) {
        return false;
      }
    }
    return true;
  }

  public static bool IsInAABBFrustum(in Plane[] planes, Vector3 min, Vector3 max) {
    for (int i = 0; i < planes.Length; i++) {
      var vec3 = new Vector3(
        planes[i].Normal.X >= 0 ? max.X : min.X,
        planes[i].Normal.Y >= 0 ? max.Y : min.Y,
        planes[i].Normal.Z >= 0 ? max.Z : min.Z
      );
      if (Vector3.Dot(planes[i].Normal, vec3) + planes[i].D < -2)
        return false;
    }
    return true;
  }
}