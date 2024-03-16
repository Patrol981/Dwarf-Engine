using System.Numerics;

namespace Dwarf.Math;
public static class Quat {
  public static Quaternion FromEuler(Vector3 euler) {
    euler *= 0.5f * (float)MathF.PI / 180f; // Convert degrees to radians and scale by 0.5

    float yaw = euler.Y;
    float pitch = euler.X;
    float roll = euler.Z;

    float cy = (float)MathF.Cos(yaw * 0.5f);
    float sy = (float)MathF.Sin(yaw * 0.5f);
    float cp = (float)MathF.Cos(pitch * 0.5f);
    float sp = (float)MathF.Sin(pitch * 0.5f);
    float cr = (float)MathF.Cos(roll * 0.5f);
    float sr = (float)MathF.Sin(roll * 0.5f);

    Quaternion q = new Quaternion();

    q.W = cr * cp * cy + sr * sp * sy;
    q.X = sr * cp * cy - cr * sp * sy;
    q.Y = cr * sp * cy + sr * cp * sy;
    q.Z = cr * cp * sy - sr * sp * cy;

    return q;
  }
}
