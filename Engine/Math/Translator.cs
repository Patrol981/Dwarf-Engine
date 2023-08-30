using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.Globals;
using Dwarf.Extensions.Logging;

namespace Dwarf.Engine.Math;
public static class Translator {
  public static OpenTK.Mathematics.Vector3 SystemNumericsToOpenTKVector(System.Numerics.Vector3 vec) {
    return new OpenTK.Mathematics.Vector3(vec.X, vec.Y, vec.Z);
  }

  public static System.Numerics.Vector3 OpenTKToSystemNumericsVector(OpenTK.Mathematics.Vector3 vec) {
    return new System.Numerics.Vector3(vec.X, vec.Y, vec.Z);
  }

  public static OpenTK.Mathematics.Quaternion SystemNumericsToOpenTKQuaternion(System.Numerics.Quaternion quat) {
    return new OpenTK.Mathematics.Quaternion(quat.X, quat.Y, quat.Z, quat.W);
  }

  public static System.Numerics.Quaternion OpenTKToSystemNumericsQuaternion(OpenTK.Mathematics.Quaternion quat) {
    return new System.Numerics.Quaternion(quat.X, quat.Y, quat.Z, quat.W);
  }
}
