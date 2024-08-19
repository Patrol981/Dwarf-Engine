using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Math;

namespace Dwarf.Model.Animation;

public class Timestamp : SkeletalAnimation {
  public Timestamp(string name) : base(name) { }
}

public class SkeletalAnimation {
  public enum Path {
    Translation,
    Rotation,
    Scale,
  };

  public enum InterpolationMethod {
    Linear,
    Step,
    CubicSpline,
  };

  public struct Channel {
    public Path Path;
    public int SamplerIndex;
    public int Node;
  }

  public struct Sampler {
    public float[] Timestamps;
    public Vector4[] TRSOutputValuesToBeInterpolated;
    public InterpolationMethod Interpolation;
  }

  public Sampler[] Samplers;
  public Channel[] Channels;

  private string _name;
  private bool _repeat;

  private float _firstKeyFrameTime;
  private float _lastKeyFrameTime;
  private float _currentKeyFrameTime = 0.0f;

  public SkeletalAnimation(string name) {
    _name = name;
  }

  public void Start() {
    _currentKeyFrameTime = _firstKeyFrameTime;
  }

  public void Stop() {
    _currentKeyFrameTime = _lastKeyFrameTime + 1.0f;
  }

  public static Vector3 Mix(Vector3 a, Vector3 b, float t) {
    return a * (1 - t) + b * t;
  }

  public void Update(float timestep, ref Skeleton skeleton) {
    if (!IsRunning()) {
      Logger.Warn($"Animation {_name} expired");
      return;
    }

    _currentKeyFrameTime += timestep;
    if (_repeat && (_currentKeyFrameTime > _lastKeyFrameTime)) {
      _currentKeyFrameTime = _firstKeyFrameTime;
    }

    foreach (var channel in Channels) {
      var sampler = Samplers[channel.SamplerIndex];

      int jointIndex = skeleton.GlobalNodeToJointIdx[channel.Node];

      for (int i = 0; i < sampler.Timestamps.Length - 1; i++) {
        if ((_currentKeyFrameTime >= sampler.Timestamps[i]) && (_currentKeyFrameTime <= sampler.Timestamps[i + 1])) {
          switch (sampler.Interpolation) {
            case InterpolationMethod.Linear:
              // float a = (_currentKeyFrameTime - sampler.Timestamps[i]) / (sampler.Timestamps[i + 1] - sampler.Timestamps[i]);
              float a = MathF.Max(0.0f, _currentKeyFrameTime - sampler.Timestamps[i]) / (sampler.Timestamps[i + 1] - sampler.Timestamps[i]);
              switch (channel.Path) {
                case Path.Translation:
                  var interpolation =
                    Vector4.Lerp(
                      sampler.TRSOutputValuesToBeInterpolated[i],
                      sampler.TRSOutputValuesToBeInterpolated[i + 1],
                      a
                    );
                  skeleton.Joints[jointIndex].DeformedNodeTranslation = new Vector3(
                    interpolation.X,
                    interpolation.Y,
                    interpolation.Z
                  );
                  break;
                case Path.Rotation:
                  var quat1 = new Quaternion(
                    sampler.TRSOutputValuesToBeInterpolated[i].W,
                    sampler.TRSOutputValuesToBeInterpolated[i].X,
                    sampler.TRSOutputValuesToBeInterpolated[i].Y,
                    sampler.TRSOutputValuesToBeInterpolated[i].Z
                  );

                  var quat2 = new Quaternion(
                    sampler.TRSOutputValuesToBeInterpolated[i + 1].W,
                    sampler.TRSOutputValuesToBeInterpolated[i + 1].X,
                    sampler.TRSOutputValuesToBeInterpolated[i + 1].Y,
                    sampler.TRSOutputValuesToBeInterpolated[i + 1].Z
                  );

                  skeleton.Joints[jointIndex].DeformedNodeRotation =
                    Quaternion.Normalize(Quaternion.Slerp(quat1, quat2, a));
                  break;
                case Path.Scale:
                  var scaleInterpolation =
                    Vector4.Lerp(
                      sampler.TRSOutputValuesToBeInterpolated[i],
                      sampler.TRSOutputValuesToBeInterpolated[i + 1],
                      a
                    );
                  skeleton.Joints[jointIndex].DeformedNodeScale = new(scaleInterpolation.X, scaleInterpolation.Y, scaleInterpolation.Z);
                  break;
                default:
                  Logger.Error("Path was not found");
                  break;
              }
              break;

            case InterpolationMethod.Step:
              Logger.Info("Step detected");
              switch (channel.Path) {
                case Path.Translation:
                  skeleton.Joints[jointIndex].DeformedNodeTranslation =
                    new Vector3(
                      sampler.TRSOutputValuesToBeInterpolated[i].X,
                      sampler.TRSOutputValuesToBeInterpolated[i].Y,
                      sampler.TRSOutputValuesToBeInterpolated[i].Z
                    );
                  break;
                case Path.Rotation:
                  var quat = new Quaternion(
                    sampler.TRSOutputValuesToBeInterpolated[i].X,
                    sampler.TRSOutputValuesToBeInterpolated[i].Y,
                    sampler.TRSOutputValuesToBeInterpolated[i].Z,
                    sampler.TRSOutputValuesToBeInterpolated[i].W
                  );
                  skeleton.Joints[jointIndex].DeformedNodeRotation = quat;
                  break;
                case Path.Scale:
                  var scale = new Vector3(
                    sampler.TRSOutputValuesToBeInterpolated[i].X,
                    sampler.TRSOutputValuesToBeInterpolated[i].Y,
                    sampler.TRSOutputValuesToBeInterpolated[i].Z
                  );
                  skeleton.Joints[jointIndex].DeformedNodeScale = scale;
                  break;
                default:
                  Logger.Error("Path was not found");
                  break;
              }
              break;
            case InterpolationMethod.CubicSpline:
              Logger.Error("Interpolation method not supported");
              break;
            default:
              Logger.Error("Interpolation method not supported");
              break;
          }
        }
      }
    }
  }

  public string Name => _name;
  public void SetFirstKeyFrameTime(float value) {
    _firstKeyFrameTime = value;
  }
  public void SetLastKeyFrameTime(float value) {
    _lastKeyFrameTime = value;
  }
  public void SetRepeat(bool value) {
    _repeat = value;
  }
  public bool WillExpire(float timestep) {
    return (!_repeat && ((_currentKeyFrameTime + timestep) > _lastKeyFrameTime));
  }
  public bool IsRunning() {
    return _repeat || (_currentKeyFrameTime <= _lastKeyFrameTime);
  }
  public float GetCurrentTime() {
    return _currentKeyFrameTime - _firstKeyFrameTime;
  }
  public float GetDuration() {
    return _lastKeyFrameTime - _firstKeyFrameTime;
  }
}