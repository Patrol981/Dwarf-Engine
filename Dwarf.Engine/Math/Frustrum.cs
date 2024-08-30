using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Rendering;

namespace Dwarf.Math;

public struct Frustum {
  public Plane Left;
  public Plane Right;
  public Plane Top;
  public Plane Bottom;
  public Plane Near;
  public Plane Far;

  public Frustum(Matrix4x4 viewProjectionMatrix) {
    // Left Plane
    Left = new Plane(
        viewProjectionMatrix.M14 + viewProjectionMatrix.M11,
        viewProjectionMatrix.M24 + viewProjectionMatrix.M21,
        viewProjectionMatrix.M34 + viewProjectionMatrix.M31,
        viewProjectionMatrix.M44 + viewProjectionMatrix.M41);

    // Right Plane
    Right = new Plane(
        viewProjectionMatrix.M14 - viewProjectionMatrix.M11,
        viewProjectionMatrix.M24 - viewProjectionMatrix.M21,
        viewProjectionMatrix.M34 - viewProjectionMatrix.M31,
        viewProjectionMatrix.M44 - viewProjectionMatrix.M41);

    // Top Plane
    Top = new Plane(
        viewProjectionMatrix.M14 - viewProjectionMatrix.M12,
        viewProjectionMatrix.M24 - viewProjectionMatrix.M22,
        viewProjectionMatrix.M34 - viewProjectionMatrix.M32,
        viewProjectionMatrix.M44 - viewProjectionMatrix.M42);

    // Bottom Plane
    Bottom = new Plane(
        viewProjectionMatrix.M14 + viewProjectionMatrix.M12,
        viewProjectionMatrix.M24 + viewProjectionMatrix.M22,
        viewProjectionMatrix.M34 + viewProjectionMatrix.M32,
        viewProjectionMatrix.M44 + viewProjectionMatrix.M42);

    // Near Plane
    Near = new Plane(
        viewProjectionMatrix.M13,
        viewProjectionMatrix.M23,
        viewProjectionMatrix.M33,
        viewProjectionMatrix.M43);

    // Far Plane
    Far = new Plane(
        viewProjectionMatrix.M14 - viewProjectionMatrix.M13,
        viewProjectionMatrix.M24 - viewProjectionMatrix.M23,
        viewProjectionMatrix.M34 - viewProjectionMatrix.M33,
        viewProjectionMatrix.M44 - viewProjectionMatrix.M43);

    // Normalize planes to ensure correct distances
    NormalizePlane(ref Left);
    NormalizePlane(ref Right);
    NormalizePlane(ref Top);
    NormalizePlane(ref Bottom);
    NormalizePlane(ref Near);
    NormalizePlane(ref Far);
  }
  private void NormalizePlane(ref Plane plane) {
    float magnitude = (float)MathF.Sqrt(plane.Normal.X * plane.Normal.X +
                                       plane.Normal.Y * plane.Normal.Y +
                                       plane.Normal.Z * plane.Normal.Z);
    plane.Normal /= magnitude;
    plane.D /= magnitude;
  }

  public static bool IsBoxInFrustum(Frustum frustum, AABB box) {
    // Check against each frustum plane
    return IsBoxInPlane(frustum.Left, box) &&
           IsBoxInPlane(frustum.Right, box) &&
           IsBoxInPlane(frustum.Top, box) &&
           IsBoxInPlane(frustum.Bottom, box) &&
           IsBoxInPlane(frustum.Near, box) &&
           IsBoxInPlane(frustum.Far, box);
  }
  private static bool IsBoxInPlane(Plane plane, AABB box) {
    // Calculate the positive and negative vertex of the bounding box relative to the plane normal
    Vector3 positiveVertex = new Vector3(
        plane.Normal.X >= 0 ? box.Max.X : box.Min.X,
        plane.Normal.Y >= 0 ? box.Max.Y : box.Min.Y,
        plane.Normal.Z >= 0 ? box.Max.Z : box.Min.Z
    );

    // If the positive vertex is outside the plane, the box is outside the frustum
    if (Vector3.Dot(plane.Normal, positiveVertex) + plane.D < 0) {
      return false;
    }

    return true;
  }

  public static List<T> FilterObjectsByFrustum<T>(ref Frustum frustum, Span<T> objects, Func<T, AABB> getBoundingBox) {
    var filteredObjects = new List<T>();

    foreach (var obj in objects) {
      AABB box = getBoundingBox(obj);

      if (IsBoxInFrustum(frustum, box)) {
        filteredObjects.Add(obj);
      }
    }

    return filteredObjects;
  }

  public static List<T> FilterObjectsByFrustum<T>(Frustum frustum, Span<T> objects) where T : IRender3DElement {
    var filteredObjects = new List<T>();

    foreach (var obj in objects) {
      if (IsBoxInFrustum(frustum, obj.GetOwner().GetComponent<MeshRenderer>().AABB)) {
        filteredObjects.Add(obj);
      }
    }

    return filteredObjects;
  }

  public static Frustum CreateFrustumFromCamera(Camera camera) {
    Frustum frustum = new();

    var halfVerticalSide = 100.0f * MathF.Tan(camera.Fov * 0.5f);
    var halfHorizontalSize = halfVerticalSide * camera.Aspect;
    var frontMultFar = 100.0f * camera.Front;

    var camPos = camera.Owner!.GetComponent<Transform>().Position;

    frustum.Near.Normal = camPos + (0.1f * camera.Front);
    frustum.Near.D = camera.Front.Z;

    frustum.Far.Normal = camPos + frontMultFar;
    frustum.Far.D = -camera.Front.Z;

    frustum.Right.Normal = camPos;
    frustum.Right.D = Vector3.Cross(frontMultFar - camera.Right * halfHorizontalSize, camera.Up).X;

    frustum.Left.Normal = camPos;
    frustum.Left.D = Vector3.Cross(camera.Up, frontMultFar + camera.Right * halfHorizontalSize).X;

    frustum.Top.Normal = camPos;
    frustum.Top.D = Vector3.Cross(camera.Right, frontMultFar + camera.Up * halfVerticalSide).Y;

    frustum.Bottom.Normal = camPos;
    frustum.Bottom.D = Vector3.Cross(frontMultFar - camera.Up * halfVerticalSide, camera.Right).Y;

    return frustum;
  }
}