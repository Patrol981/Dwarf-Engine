using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering;

using OpenTK.Mathematics;

namespace Dwarf.Engine.Math;

public static class Converter {
  public static float DegreesToRadians(float deg) {
    float rad = (MathF.PI / 180) * deg;
    return (rad);
  }

  public static float RadiansToDegrees(float rad) {
    float deg = (180 / MathF.PI) * rad;
    return deg;
  }

  public static OpenTK.Mathematics.Vector3 QuaternionToEulerAngles(System.Numerics.Quaternion quat) {
    float sqw = quat.W * quat.W;
    float sqx = quat.X * quat.X;
    float sqy = quat.Y * quat.Y;
    float sqz = quat.Z * quat.Z;

    // Assuming the quaternion is normalized (otherwise divide by its magnitude)
    float unit = sqx + sqy + sqz + sqw;
    float test = quat.X * quat.W - quat.Y * quat.Z;

    OpenTK.Mathematics.Vector3 euler;
    // Singularities at the poles
    if (test > 0.4995f * unit) {
      euler.Y = 2.0f * (float)MathHelper.Atan2(quat.Y, quat.X);
      euler.X = (float)MathHelper.Pi / 2.0f;
      euler.Z = 0;
      return euler;
    }
    if (test < -0.4995f * unit) {
      euler.Y = -2.0f * (float)MathHelper.Atan2(quat.Y, quat.X);
      euler.X = -(float)MathHelper.Pi / 2.0f;
      euler.Z = 0;
      return euler;
    }

    // Compute Euler angles
    euler.Y = (float)MathHelper.Atan2(2.0f * quat.Y * quat.W + 2.0f * quat.X * quat.Z, 1.0f - 2.0f * (sqz + sqw));
    euler.X = (float)MathHelper.Asin(2.0f * test / unit);
    euler.Z = (float)MathHelper.Atan2(2.0f * quat.X * quat.W + 2.0f * quat.Y * quat.Z, 1.0f - 2.0f * (sqx + sqy));

    return euler;
  }
}