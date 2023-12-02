using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Physics;
using Dwarf.Extensions.Logging;

using static Assimp.Metadata;

namespace Dwarf.Engine.Math;
public class Ray {
  public class RayResult {
    public Vector3 RayOrigin { get; set; }
    public Vector3 RayDirection { get; set; }
    public Vector3 RayOriginRaw { get; set; }
    public Vector3 RayDirectionRaw { get; set; }
  }

  public class RaycastHitResult {
    public bool Present { get; set; }
    public Vector3 Point { get; set; }
    public float Distance { get; set; }
  }

  public record Plane(Vector3 Normal, Vector3 Point);

  public static Vector2 MouseToWorld2D(Camera camera, Vector2 screenSize) {
    var mousePos = MouseState.GetInstance().MousePosition;
    float normalizedX = (2.0f * (float)mousePos.X) / screenSize.X - 1.0f;
    float normalizedY = 1.0f - (2.0f * (float)mousePos.Y) / screenSize.Y;

    Matrix4x4.Invert(camera.GetProjectionMatrix(), out var unProject);
    Vector4 nearPoint = new Vector4(normalizedX, normalizedY, 0.0f, 1.0f);
    Vector4 worldPoint = Vector4.Transform(nearPoint, unProject);
    var tmp = new Vector2();
    tmp.X = worldPoint.X / worldPoint.W;
    tmp.Y = worldPoint.Y / worldPoint.W;

    tmp.X *= 100;
    tmp.Y *= -100;
    return tmp;
  }

  [Obsolete]
  private static Vector3 CalcuateRay(Camera camera, Vector2 screenSize) {
    var mousePos = MouseState.GetInstance().MousePosition;
    float normalizedX = (2.0f * (float)mousePos.X) / screenSize.X - 1.0f;
    float normalizedY = -(1.0f - (2.0f * (float)mousePos.Y) / screenSize.Y);

    Vector3 rayNds = new Vector3(normalizedX, normalizedY, 1.0f);
    Vector4 rayClip = new Vector4(rayNds.X, rayNds.Y, rayNds.Z, 1.0f);

    Matrix4x4.Invert(camera.GetProjectionMatrix(), out var inverseProjection);
    Vector4 rayEye = Vector4.Transform(rayClip, inverseProjection);
    rayEye.Z = -1.0f;
    rayEye.W = 0.0f;

    Matrix4x4.Invert(camera.GetViewMatrix(), out var inverseViewMatrix);
    Vector3 rayWorld = Vector3.Transform(new(rayEye.X, rayEye.Y, rayEye.Z), inverseViewMatrix);
    rayWorld = Vector3.Normalize(rayWorld);

    return rayWorld;
  }

  public static RayResult GetRayInfo2(Camera camera, Vector2 screenSize) {
    var mousePos = MouseState.GetInstance().MousePosition;
    var ndcX = (float)(2.0f * mousePos.X) / screenSize.X - 1.0f;
    var ndcY = 1.0f - (float)(2.0f * mousePos.Y) / screenSize.Y;

    var endResult = new RayResult();
    var rayStartView = new Vector4(ndcX, ndcY, -1.0f, 1.0f);
    var rayEndView = new Vector4(ndcX, ndcY, 1.0f, 1.0f);

    Matrix4x4.Invert(camera.GetViewMatrix(), out var rayStartWorld);
    Matrix4x4.Invert(camera.GetViewMatrix(), out var rayEndWorld);

    var r1 = Vector4.Transform(rayStartView, rayStartWorld);
    var r2 = Vector4.Transform(rayEndView, rayEndWorld);

    endResult.RayOrigin = new(r1.X, r1.Y, r1.Z);
    endResult.RayDirection = new(r2.X, r2.Y, r2.Z);
    return endResult;
  }

  public static RayResult GetRayInfo(Camera camera, Vector2 screenSize) {
    var mousePos = MouseState.GetInstance().MousePosition;
    var rayStartNDC = new Vector4(
      ((float)mousePos.X / (float)screenSize.X - 0.5f) * 2.0f,
      ((float)mousePos.Y / (float)screenSize.Y - 0.5f) * 2.0f,
      -1.0f,
      1.0f
    );
    var rayEndNDC = new Vector4(
      ((float)mousePos.X / (float)screenSize.X - 0.5f) * 2.0f,
      ((float)mousePos.Y / (float)screenSize.Y - 0.5f) * 2.0f,
      0.0f,
      1.0f
    );

    Matrix4x4.Invert(camera.GetProjectionMatrix(), out var inverseProjection);
    Matrix4x4.Invert(camera.GetViewMatrix(), out var inverseViewMatrix);

    var rayStartCamera = Vector4.Transform(rayStartNDC, inverseProjection);
    rayStartCamera /= rayStartCamera.W;

    var rayStartWorld = Vector4.Transform(rayStartCamera, inverseViewMatrix);
    rayStartWorld /= rayStartWorld.W;

    var rayEndCamera = Vector4.Transform(rayEndNDC, inverseProjection);
    rayEndCamera /= rayEndCamera.W;

    var rayEndWorld = Vector4.Transform(rayEndCamera, inverseViewMatrix);
    rayEndWorld /= rayEndWorld.W;

    var rayDirWorld = rayEndWorld - rayStartWorld;
    rayDirWorld = Vector4.Normalize(rayDirWorld);

    var result = new RayResult();
    result.RayOrigin = new(rayStartWorld.X, rayStartWorld.Y, rayStartWorld.Z);
    result.RayDirection = Vector3.Normalize(new(rayDirWorld.X, rayDirWorld.Y, rayDirWorld.Z));
    result.RayOriginRaw = new(rayStartWorld.X, rayStartWorld.Y, rayStartWorld.Z);
    result.RayDirectionRaw = new(rayDirWorld.X, rayDirWorld.Y, rayDirWorld.Z);

    // Logger.Warn($"[Ray Origin] {result.RayOrigin}");
    // Logger.Warn($"[Ray Dir NRM] {result.RayDirection}");

    return result;
  }

  public static RaycastHitResult CastRay(float maxDistance) {
    var camera = CameraState.GetCamera();
    var screenSize = ApplicationState.Instance.Window.Extent;

    var rayData = GetRayInfo(camera, new(screenSize.width, screenSize.height));

    var hitResult = new RaycastHitResult();
    hitResult.Present = false;
    hitResult.Point = rayData.RayOrigin + rayData.RayDirection * maxDistance;

    return hitResult;
  }

  public static RaycastHitResult MeshIntersection(Entity entity) {
    var camera = CameraState.GetCamera();
    var screenSize = ApplicationState.Instance.Window.Extent;
    var rayData = GetRayInfo(camera, new(screenSize.width, screenSize.height));

    var model = entity.GetComponent<Model>();
    if (!entity.HasComponent<ColliderMesh>()) return new();
    var mesh = entity.GetComponent<ColliderMesh>().Mesh;

    var endInfo = new RaycastHitResult();

    for (int i = 0; i < mesh.Vertices.Length; i += 3) {
      var vertex0 = mesh.Vertices[i];
      var vertex1 = mesh.Vertices[i + 1];
      var vertex2 = mesh.Vertices[i + 2];

      var normal = Vector3.Normalize(
        Vector3.Cross(vertex1.Position - vertex0.Position, vertex2.Position - vertex0.Position)
      );

      var plane = new Plane(normal, vertex0.Position);
      if (PlaneIntersection(rayData.RayOrigin, rayData.RayDirection, plane)) {
        var point = GetIntersectionPoint(rayData.RayOrigin, rayData.RayDirection, plane);

        if (PointInsideTriangle(point, vertex0, vertex1, vertex2)) {
          Logger.Info($"Intersection! {entity.Name}");
          endInfo.Point = point;
          endInfo.Present = true;
          return endInfo;
        }
      }
    }

    return endInfo;
  }

  private static bool PlaneIntersection(Vector3 rayStart, Vector3 rayEnd, Plane plane) {
    var rayDir = Vector3.Normalize(rayEnd - rayStart);
    var denominator = Vector3.Dot(rayDir, plane.Normal);

    if (MathF.Abs(denominator) < float.Epsilon) {
      return false;
    }

    var t = Vector3.Dot(plane.Point - rayStart, plane.Normal) / denominator;

    return t >= 0.0;
  }

  private static Vector3 GetIntersectionPoint(Vector3 rayStart, Vector3 rayEnd, Plane plane) {
    var rayDir = Vector3.Normalize(rayEnd - rayStart);
    var t = Vector3.Dot(plane.Point - rayStart, plane.Normal) / Vector3.Dot(rayDir, plane.Normal);
    return rayStart + t * rayDir;
  }

  private static bool PointInsideTriangle(Vector3 point, Vertex v0, Vertex v1, Vertex v2) {
    var edge0 = v1.Position - v0.Position;
    var edge1 = v2.Position - v1.Position;
    var edge2 = v0.Position - v2.Position;

    var normal0 = Vector3.Cross(edge0, point - v0.Position);
    var normal1 = Vector3.Cross(edge1, point - v1.Position);
    var normal2 = Vector3.Cross(edge2, point - v2.Position);

    if (Vector3.Dot(normal0, normal1) > 0.0 && Vector3.Dot(normal1, normal2) > 0.0) {
      return true;
    }

    return false;
  }

  public static RaycastHitResult OBBIntersection(Entity entity, float maxDistance) {
    var camera = CameraState.GetCamera();
    var screenSize = ApplicationState.Instance.Window.Extent;
    var rayData = GetRayInfo(camera, new(screenSize.width, screenSize.height));

    var transform = entity.GetComponent<Transform>();
    var model = entity.GetComponent<Model>();

    float tMin = 0.0f;
    float tMax = maxDistance;

    var modelMatrix = transform.Matrix4;
    var positionWorldspace = new Vector3(modelMatrix[3, 0], modelMatrix[3, 1], modelMatrix[3, 2]);
    var delta = positionWorldspace - rayData.RayOrigin;

    var hitResult = new RaycastHitResult();
    hitResult.Present = false;
    hitResult.Point = Vector3.Zero;

    var xAxis = new Vector3(modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
    var yAxis = new Vector3(modelMatrix[1, 0], modelMatrix[1, 1], modelMatrix[1, 2]);
    var zAxis = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
    float eX, eY, eZ;
    float fX, fY, fZ;

    eX = Vector3.Dot(xAxis, delta);
    fX = Vector3.Dot(xAxis, rayData.RayDirection);

    eY = Vector3.Dot(yAxis, delta);
    fY = Vector3.Dot(yAxis, rayData.RayDirection);

    eZ = Vector3.Dot(zAxis, delta);
    fZ = Vector3.Dot(zAxis, rayData.RayDirection);

    float t1X = (eX + model.AABB.Min.X) / fX;
    float t2X = (eX + model.AABB.Max.X) / fX;

    float t1Y = (eY + model.AABB.Min.Y) / fY;
    float t2Y = (eY + model.AABB.Max.Y) / fY;

    float t1Z = (eZ + model.AABB.Min.Z) / fZ;
    float t2Z = (eZ + model.AABB.Max.Z) / fZ;

    if (t2X < tMax) tMax = t2X;
    if (t1X > tMin) tMin = t1X;

    if (tMax > tMin) {
      hitResult.Present = true;
      Logger.Error("Present X");
    }

    if (t2Y < tMax) tMax = t2Y;
    if (t1Y > tMin) tMin = t1Y;

    if (tMax > tMin) {
      hitResult.Present = true;
      Logger.Error("Present Y");
    }

    if (t2Z < tMax) tMax = t2Z;
    if (t1Z > tMin) tMin = t1Z;

    if (tMax > tMin) {
      hitResult.Present = true;
      Logger.Error("Present Z");
    }

    return hitResult;
  }

  public static RaycastHitResult OBBIntersection_Base(Entity entity, float maxDistance) {
    var camera = CameraState.GetCamera();
    var screenSize = ApplicationState.Instance.Window.Extent;

    var rayData = GetRayInfo(camera, new(screenSize.width, screenSize.height));

    var transform = entity.GetComponent<Transform>();
    var model = entity.GetComponent<Model>();

    float tMin = 0.0f;
    // float tMax = 100000.0f;
    float tMax = maxDistance;

    var modelMatrix = transform.Matrix4;
    var positionWorldspace = new Vector3(modelMatrix[3, 0], modelMatrix[3, 1], modelMatrix[3, 2]);
    // var positionWorldspace = new Vector3(modelMatrix[0, 3], modelMatrix[1, 3], modelMatrix[2, 3]);
    var delta = positionWorldspace - rayData.RayOrigin;

    var collisionPoint = Vector3.Zero;
    var hitResult = new RaycastHitResult();
    hitResult.Present = false;
    hitResult.Point = collisionPoint;

    {
      var xAxis = new Vector3(modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
      // var xAxis = new Vector3(modelMatrix[0, 0], modelMatrix[1, 0], modelMatrix[2, 0]);
      float e = Vector3.Dot(xAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, xAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.X) / f;
        float t2 = (e + model.AABB.Max.X) / f;

        if (t1 > t2) { float w = t1; t1 = t2; t2 = w; }

        if (t2 < tMax) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t2;
          tMax = t2;
        }

        if (t1 > tMin) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t1;
          tMin = t1;
        }

        if (tMax < tMin)
          return hitResult;
      } else {
        if (-e + model.AABB.Min.X > 0.0f || -e + model.AABB.Max.X < 0.0f) {
          return hitResult;
        }
      }
    }

    {
      var yAxis = new Vector3(modelMatrix[1, 0], modelMatrix[1, 1], modelMatrix[1, 2]);
      // var yAxis = new Vector3(modelMatrix[0, 1], modelMatrix[1, 1], modelMatrix[2, 1]);
      float e = Vector3.Dot(yAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, yAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.Y) / f;
        float t2 = (e + model.AABB.Max.Y) / f;

        if (t1 > t2) { float w = t1; t1 = t2; t2 = w; }

        if (t2 < tMax) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t2;
          tMax = t2;
        }

        if (t1 > tMin) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t1;
          tMin = t1;
        }

        if (tMin > tMax)
          return hitResult;
      } else {
        if (-e + model.AABB.Min.Y > 0.0f || -e + model.AABB.Max.Y < 0.0f) {
          Logger.Info("Y case");
          return hitResult;
        }
      }
    }

    {
      var zAxis = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
      // var zAxis = new Vector3(modelMatrix[0, 2], modelMatrix[1, 2], modelMatrix[2, 2]);
      float e = Vector3.Dot(zAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, zAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.Z) / f;
        float t2 = (e + model.AABB.Max.Z) / f;
        if (t1 > t2) { float w = t1; t1 = t2; t2 = w; }

        if (t2 < tMax) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t2;
          tMax = t2;
        }

        if (t1 > tMin) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t1;
          tMin = t1;
        }

        if (tMin > tMax)
          return hitResult;
      } else {
        if (-e + model.AABB.Min.Z > 0.0f || -e + model.AABB.Max.Z < 0.0f) {
          return hitResult;
        }
      }
    }

    Logger.Info("INTERSECTION");
    hitResult.Present = true;
    hitResult.Distance = tMin;
    Logger.Error($"POINT: {hitResult.Point}");
    Logger.Warn(hitResult.Distance);
    return hitResult;
  }

  public static Vector3 MouseToWorld3D(float maxDistance) {
    var camera = CameraState.GetCamera();
    var screenSize = ApplicationState.Instance.Window.Extent;
    var rayDirection = CalcuateRay(camera, new(screenSize.width, screenSize.height));
    var rayOrigin = camera.Owner!.GetComponent<Transform>().Position;

    // tmp.X *= 1000;
    // tmp.Y = 0;
    // tmp.Z *= 1000;
    // tmp.Z *= 100;

    // rayWorld.X *= 100;
    // rayWorld.Y = 0.0f;

    // Logger.Info(rayDirection);
    return rayDirection;
  }

  public static Vector2 ScreenPointToWorld2D(Camera camera, Vector2 point, Vector2 screenSize) {
    float normalizedX = (2.0f * point.X) / screenSize.X - 1.0f;
    float normalizedY = 1.0f - (2.0f * point.Y) / screenSize.Y;

    Matrix4x4.Invert(camera.GetProjectionMatrix(), out var unProject);
    Vector4 nearPoint = new Vector4(normalizedX, normalizedY, 0.0f, 1.0f);
    Vector4 worldPoint = Vector4.Transform(nearPoint, unProject);
    var tmp = new Vector2();
    tmp.X = worldPoint.X / worldPoint.W;
    tmp.Y = worldPoint.Y / worldPoint.W;

    tmp.X *= 100;
    tmp.Y *= -100;
    return tmp;
  }
}
