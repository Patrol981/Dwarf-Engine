using System.Numerics;
using Dwarf.Loaders;

namespace Dwarf.Model.Animation;

public class AnimationSampler : ICloneable {
  public enum InterpolationType {
    Linear,
    Step,
    CubicSpline
  }

  public InterpolationType Interpolation;
  public List<float> Inputs = [];
  public List<Vector4> OutputsVec4 = [];
  public List<float> Outputs = [];

  public Vector4 CubicSplineInterpolation(int idx, float time, int stride) {
    float delta = Inputs[idx + 1] - Inputs[idx];
    float t = (time - Inputs[idx]) / delta;
    var current = idx * stride * 3;
    var next = (idx + 1) * stride * 3;
    var A = 0;
    var V = stride * 1;
    var B = stride * 2;

    float t2 = MathF.Pow(t, 2);
    float t3 = MathF.Pow(t, 3);
    var pt = Vector4.Zero;
    for (int i = 0; i < stride; i++) {
      float p0 = Outputs[current + i + V];      // starting point at t = 0
      float m0 = delta * Outputs[current + i + A];  // scaled starting tangent at t = 0
      float p1 = Outputs[next + i + V];       // ending point at t = 1
      float m1 = delta * Outputs[next + i + B];   // scaled ending tangent at t = 1
      pt[i] = ((2.0f * t3 - 3.0f * t2 + 1.0f) * p0) + ((t3 - 2.0f * t2 + t) * m0) + ((-2.0f * t3 + 3.0f * t2) * p1) + ((t3 - t2) * m0);
    }
    return pt;
  }

  public void Translate(int idx, float time, Node node, float weight) {
    var blendedTranslation = node.Translation;
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        var newTranslation = Vector4.Lerp(OutputsVec4[idx], OutputsVec4[idx + 1], u).ToVector3();
        node.Translation = Vector3.Lerp(blendedTranslation, newTranslation, weight);
        // node.Translation = newTranslation;
        break;
      case InterpolationType.Step:
        newTranslation = OutputsVec4[idx].ToVector3();
        // node.Translation = Vector3.Lerp(blendedTranslation, newTranslation, weight);
        node.Translation = newTranslation;
        break;
      case InterpolationType.CubicSpline:
        newTranslation = CubicSplineInterpolation(idx, time, 3).ToVector3();
        node.Translation = Vector3.Lerp(blendedTranslation, newTranslation, weight);
        // node.Translation = newTranslation;
        break;
    }
  }

  public void Scale(int idx, float time, Node node, float weight) {
    var blendedScale = node.Scale;
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        var newScale = Vector4.Lerp(OutputsVec4[idx], OutputsVec4[idx + 1], u).ToVector3();
        node.Scale = Vector3.Lerp(blendedScale, newScale, weight);
        // node.Scale = newScale;
        break;
      case InterpolationType.Step:
        newScale = OutputsVec4[idx].ToVector3();
        node.Scale = Vector3.Lerp(blendedScale, newScale, weight);
        // node.Scale = newScale;
        break;
      case InterpolationType.CubicSpline:
        newScale = CubicSplineInterpolation(idx, time, 3).ToVector3();
        node.Scale = Vector3.Lerp(blendedScale, newScale, weight);
        // node.Scale = newScale;
        break;
    }
  }

  public void Rotate(int idx, float time, Node node, float weight) {
    var blendedRotation = node.Rotation;
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        var quat1 = new Quaternion(
          OutputsVec4[idx].X,
          OutputsVec4[idx].Y,
          OutputsVec4[idx].Z,
          OutputsVec4[idx].W
        );
        var quat2 = new Quaternion(
          OutputsVec4[idx + 1].X,
          OutputsVec4[idx + 1].Y,
          OutputsVec4[idx + 1].Z,
          OutputsVec4[idx + 1].W
        );
        var newRotation = Quaternion.Slerp(quat1, quat2, u);
        node.Rotation = Quaternion.Slerp(blendedRotation, Quaternion.Normalize(newRotation), weight);

        // node.Rotation = Quaternion.Normalize(Quaternion.Slerp(quat1, quat2, u));
        break;
      case InterpolationType.Step:
        newRotation = new Quaternion(
          OutputsVec4[idx].X,
          OutputsVec4[idx].Y,
          OutputsVec4[idx].Z,
          OutputsVec4[idx].W
        );
        node.Rotation = Quaternion.Slerp(blendedRotation, newRotation, weight);
        // node.Rotation = newRotation;
        break;
      case InterpolationType.CubicSpline:
        var rot = CubicSplineInterpolation(idx, time, 4);
        newRotation = new Quaternion(
          rot.X,
          rot.Y,
          rot.Z,
          rot.W
        );
        node.Rotation = Quaternion.Slerp(blendedRotation, Quaternion.Normalize(newRotation), weight);
        // node.Rotation = Quaternion.Normalize(newRotation);
        break;
    }
  }

  public object Clone() {
    return new AnimationSampler {
      Interpolation = Interpolation,
      Inputs = Inputs,
      OutputsVec4 = OutputsVec4,
      Outputs = Outputs,
    };
  }
}
