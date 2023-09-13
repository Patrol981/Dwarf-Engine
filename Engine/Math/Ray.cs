using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Logging;

namespace Dwarf.Engine.Math;
public class Ray {
  internal class RayResult {
    internal Vector3 RayOrigin { get; set; }
    internal Vector3 RayDirection { get; set; }
  }

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

  private static RayResult GetRayInfo(Camera camera, Vector2 screenSize) {
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
    return result;
  }

  public static bool OBBIntersection(Entity entity, float maxDistance) {
    var camera = CameraState.GetCamera();
    var screenSize = ApplicationState.Instance.Window.Extent;

    var rayData = GetRayInfo(camera, new(screenSize.width, screenSize.height));

    var transform = entity.GetComponent<Transform>();
    var model = entity.GetComponent<Model>();

    float tMin = 0.0f;
    float tMax = 100000.0f;

    var modelMatrix = transform.Matrix4;
    var positionWorldspace = new Vector3(modelMatrix[3, 0], modelMatrix[3, 1], modelMatrix[3, 2]);
    var delta = positionWorldspace - rayData.RayOrigin;

    {
      var xAxis = new Vector3(modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
      float e = Vector3.Dot(xAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, xAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.X) / f;
        float t2 = (e + model.AABB.Max.X) / f;

        if (t1 > t2) { float w = t1; t1 = t2; t2 = w; }

        if (t2 < tMax)
          tMax = t2;
        if (t1 > tMin)
          tMin = t1;
        if (tMax < tMin)
          return false;
      } else {
        if (-e + model.AABB.Min.X > 0.0f || -e + model.AABB.Max.X < 0.0f) {
          return false;
        }
      }
    }

    {
      var yAxis = new Vector3(modelMatrix[1, 0], modelMatrix[1, 1], modelMatrix[1, 2]);
      float e = Vector3.Dot(yAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, yAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.Y) / f;
        float t2 = (e + model.AABB.Max.Y) / f;

        if (t1 > t2) { float w = t1; t1 = t2; t2 = w; }

        if (t2 < tMax)
          tMax = t2;
        if (t1 > tMin)
          tMin = t1;
        if (tMin > tMax)
          return false;
      } else {
        if (-e + model.AABB.Min.Y > 0.0f || -e + model.AABB.Max.Y < 0.0f) {
          return false;
        }
      }
    }

    {
      var zAxis = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
      float e = Vector3.Dot(zAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, zAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.Z) / f;
        float t2 = (e + model.AABB.Max.Z) / f;
        if (t1 > t2) { float w = t1; t1 = t2; t2 = w; }

        if (t2 < tMax)
          tMax = t2;
        if (t1 > tMin)
          tMin = t1;
        if (tMin > tMax)
          return false;
      } else {
        if (-e + model.AABB.Min.Z > 0.0f || -e + model.AABB.Max.Z < 0.0f) {
          return false;
        }
      }
    }

    Logger.Info("INTERSECTION");
    return true;
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
