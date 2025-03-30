using System.Numerics;
using Dwarf.Globals;
using Dwarf.Windowing;

namespace Dwarf.Math;

public static class Converter {
  public static float DegreesToRadians(float deg) {
    float rad = MathF.PI / 180 * deg;
    return rad;
  }

  public static float RadiansToDegrees(float rad) {
    float deg = 180 / MathF.PI * rad;
    return deg;
  }

  public static Vector2I ToVec2I(this Vector2 vec2) {
    return new Vector2I((int)vec2.X, (int)vec2.Y);
  }

  public static Vector2 FromVec2I(this Vector2I vec2) {
    return new Vector2(vec2.X, vec2.Y);
  }

  public static Vector4I ToVec4I(this Vector4 vec4) {
    return new Vector4I((int)vec4.X, (int)vec4.Y, (int)vec4.Z, (int)vec4.W);
  }

  public static Vector2 WorldToScreen(Vector3 worldPos) {
    var viewProj = CameraState.GetCamera().GetViewMatrix() * CameraState.GetCamera().GetProjectionMatrix();
    var screen = Application.Instance.Window.Size;

    // Transform world position by the combined View*Projection matrix
    Vector4 clipSpacePos = Vector4.Transform(new Vector4(worldPos, 1.0f), viewProj);

    // If w is near-zero or negative, the point is behind the camera or at infinity
    if (clipSpacePos.W < 0.0001f) {
      // Return something indicating off-screen, so you can skip drawing
      return new Vector2(-10000, -10000);
    }

    // Perspective divide to get Normalized Device Coordinates (NDC) in [-1..1]
    clipSpacePos /= clipSpacePos.W;
    // clipSpacePos.X, clipSpacePos.Y, clipSpacePos.Z now in [-1..1]

    // Some Vulkan conventions put +1 at the top of NDC Y,
    // so to map that to a typical UI coordinate system with (0,0) at top-left:
    float ndcX = clipSpacePos.X;  // -1 is left edge, +1 is right edge
    float ndcY = clipSpacePos.Y;  // +1 is top edge, -1 is bottom edge

    // Convert from NDC to [0..screenWidth] and [0..screenHeight]
    float screenX = (ndcX + 1.0f) * 0.5f * screen.X;
    float screenY = (1.0f - ndcY) * 0.5f * screen.Y;

    // If your Vulkan pipeline actually ends with +1 at BOTTOM, you might do
    //   float screenY = (ndcY + 1.0f) * 0.5f * screenHeight;
    // instead. It depends on how you set up your matrices and viewport.

    return new Vector2(screenX, screenY);
  }
}