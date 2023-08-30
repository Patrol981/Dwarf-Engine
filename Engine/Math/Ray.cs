using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.Globals;

namespace Dwarf.Engine.Math;
public class Ray {
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
