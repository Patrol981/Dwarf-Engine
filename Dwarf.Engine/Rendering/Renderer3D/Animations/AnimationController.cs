using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.Rendering.Renderer3D.Animations;

public class AnimationController : Component {
  private MeshRenderer _meshRenderer;
  private readonly Dictionary<string, Animation> _animations = [];
  // private Animation _currentAnimation = null!;
  private readonly float _tickRate = 0.0f;
  private List<(Animation Animation, float Weight)> _activeAnimations = [];

  public AnimationController() {
    var mr = Owner?.TryGetComponent<MeshRenderer>();
    if (mr != null) {
      _meshRenderer = mr;
    } else {
      _meshRenderer = new MeshRenderer();
    }
    _tickRate = 1.0f / Application.Instance.Window.RefreshRate;
  }
  public void Init(MeshRenderer meshRenderer) {
    _meshRenderer = meshRenderer;

    foreach (var animation in meshRenderer.Animations) {
      _animations.TryAdd(animation.Name, animation);
    }
  }

  public Animation? GetAnimation(string name) {
    _animations.TryGetValue(name, out var animation);
    if (animation != null) {
      return animation;
    }
    Logger.Error("No animation found");
    return null;
  }

  public void SetFirstAnimation() {
    if (_animations.Count < 1) return;
    // _currentAnimation = _animations.First().Value;
    _activeAnimations.Clear();
    _activeAnimations.Add((_animations.First().Value, 1.0f));
  }

  public void PlayFirstAnimation() {
    for (int i = 0; i < _activeAnimations.Count; i++) {
      _activeAnimations[i] = (_activeAnimations[i].Animation, 0f);
    }
    _activeAnimations[0] = (_activeAnimations[0].Animation, 1.0f);
  }

  public void PlayAnimation(string animationName, float weight) {
    for (int i = 0; i < _activeAnimations.Count; i++) {
      if (_activeAnimations[i].Animation.Name != animationName) {
        _activeAnimations[i] = (_activeAnimations[i].Animation, 0f);
      }
    }

    var index = _activeAnimations.FindIndex(x => x.Animation.Name == animationName);
    if (index != -1) {
      _activeAnimations[index] = (_activeAnimations[index].Animation, weight);
    }
  }

  public void PlayAnimations((string animationName, float weight)[] animations) {
    var newActiveAnimations = new List<(Animation Animation, float Weight)>();
    foreach (var (animationName, weight) in animations) {
      if (_animations.TryGetValue(animationName, out var animation)) {
        newActiveAnimations.Add((animation, weight));
      }
    }
    _activeAnimations.Clear();
    _activeAnimations.AddRange(newActiveAnimations);
  }

  public void PlayAnimations_Old((string animationName, float weight)[] animations) {
    for (int i = 0; i < _activeAnimations.Count; i++) {
      var activeAnimationName = _activeAnimations[i].Animation.Name;

      if (!animations.Any(anim => anim.animationName == activeAnimationName)) {
        _activeAnimations[i] = (_activeAnimations[i].Animation, 0f);
      }
    }

    foreach (var (animationName, weight) in animations) {
      var index = _activeAnimations.FindIndex(x => x.Animation.Name == animationName);
      if (index != -1) {
        _activeAnimations[index] = (_activeAnimations[index].Animation, weight);
      }
    }
  }

  public void SetCurrentAnimation(string animationName, float weight = 1.0f) {
    _animations.TryGetValue(animationName, out var animation);
    if (animation == null) {
      Logger.Error($"Animation {animationName} is not found.");
      return;
    }

    // _currentAnimation = animation;

    // _activeAnimations.Clear();
    _activeAnimations.Add((animation, weight));
  }

  public void Update(Node node) {
    if (node == null) return;
    if (_activeAnimations.Count < 1) return;
    List<(Animation Animation, float Weight)> clone = [.. _activeAnimations];

    node.AnimationTimer += Time.DeltaTimeRender;
    float totalWeight = 0f;

    for (int i = 0; i < clone.Count; i++) {
      totalWeight += clone[i].Weight;
    }

    totalWeight = MathF.Max(totalWeight, 1e-6f); // Avoid division by zero

    for (int i = 0; i < clone.Count; i++) {
      if (i >= clone.Count) break; // Safety check to prevent out-of-range errors

      var animationWeightPair = clone[i];
      var animation = animationWeightPair.Animation;
      var weight = animationWeightPair.Weight;

      if (animation == null) break;

      float normalizedWeight = weight / totalWeight;
      float adjustedTimer = node.AnimationTimer;
      if (animation.End > 0) {
        adjustedTimer %= animation.End; // Loop the animation timer
      }
      UpdateAnimation(animation, adjustedTimer, normalizedWeight);
    }
  }

  public void Update_Old(Node node) {
    // if (_currentAnimation == null) return;
    if (_activeAnimations.Count < 1) return;
    node.AnimationTimer += Time.DeltaTimeRender;

    // var currentAnimation = _activeAnimations.MaxBy(x => x.Weight);
    (Animation Animation, float Weight) currentAnimation = (null!, -1);
    float weightSum = 0.0f;

    for (int i = 0; i < _activeAnimations.Count; i++) {
      weightSum += _activeAnimations[i].Weight;
      if (_activeAnimations[i].Weight > currentAnimation.Weight) {
        currentAnimation = _activeAnimations[i];
      }
    }

    if (node.AnimationTimer > currentAnimation.Animation.End) {
      node.AnimationTimer -= currentAnimation.Animation.End;
    }

    UpdateAnimation(currentAnimation.Animation, node.AnimationTimer, weightSum);

    // if (node.AnimationTimer > _currentAnimation.End) {
    //   node.AnimationTimer -= _currentAnimation.End;
    // }

    // UpdateAnimation(_currentAnimation, node.AnimationTimer, 1);
  }

  public void UpdateAnimation(Animation animation, float time, float weight) {
    foreach (var channel in animation.Channels) {
      var sampler = animation.Samplers[channel.SamplerIndex];
      if (sampler.Inputs.Count > sampler.OutputsVec4.Count) {
        continue;
      }
      for (int i = 0; i < sampler.Inputs.Count - 1; i++) {
        if ((time >= sampler.Inputs[i]) && (time <= sampler.Inputs[i + 1])) {
          float u = MathF.Max(0.0f, time - sampler.Inputs[i]) / (sampler.Inputs[i + 1] - sampler.Inputs[i]);
          if (u <= 1.0f) {
            switch (channel.Path) {
              case AnimationChannel.PathType.Translation:
                sampler.Translate(i, time, channel.Node, weight);
                break;
              case AnimationChannel.PathType.Rotation:
                sampler.Rotate(i, time, channel.Node, weight);
                break;
              case AnimationChannel.PathType.Scale:
                sampler.Scale(i, time, channel.Node, weight);
                break;
            }
          }
        }
      }
    }

    foreach (var node in _meshRenderer.Nodes) {
      node.Update();
    }
  }

  public void UpdateAnimation_Old(Animation animation, float time, float weight) {
    bool updated = false;
    foreach (var channel in animation.Channels) {
      var sampler = animation.Samplers[channel.SamplerIndex];
      if (sampler.Inputs.Count > sampler.OutputsVec4.Count) {
        continue;
      }
      for (int i = 0; i < sampler.Inputs.Count - 1; i++) {
        if ((time >= sampler.Inputs[i]) && (time <= sampler.Inputs[i + 1])) {
          float u = MathF.Max(0.0f, time - sampler.Inputs[i]) / (sampler.Inputs[i + 1] - sampler.Inputs[i]);
          if (u <= 1.0f) {
            switch (channel.Path) {
              case AnimationChannel.PathType.Translation:
                sampler.Translate(i, time, channel.Node, weight);
                break;
              case AnimationChannel.PathType.Rotation:
                sampler.Rotate(i, time, channel.Node, weight);
                break;
              case AnimationChannel.PathType.Scale:
                sampler.Scale(i, time, channel.Node, weight);
                break;
            }

            if (channel.Node.ParentRenderer != null) {
              var target = channel.Node.ParentRenderer.AddedNodes.Where(x => x.Value == channel.Node).ToArray();
              if (target.Length > 0) {
                // Logger.Info(target);
                foreach (var t in target) {
                  t.Key.Translation = t.Value.Translation + t.Key.TranslationOffset;
                  // t.Key.Rotation.X = t.Value.Rotation.X + t.Key.RotationOffset.X;
                  // t.Key.Rotation.Y = t.Value.Rotation.Y + t.Key.RotationOffset.Y;
                  // t.Key.Rotation.Z = t.Value.Rotation.Z + t.Key.RotationOffset.Z;
                  t.Key.Rotation = Quaternion.Normalize(t.Value.Rotation) + Quaternion.Normalize(t.Key.RotationOffset);
                  // t.Key.Rotation.W = t.Value.Rotation.W + t.Key.RotationOffset.W;
                  // t.Key.Scale = t.Value.Scale;
                }
              }
            }

            updated = true;
          }
        }
      }
    }
    if (updated) {
      foreach (var node in _meshRenderer.Nodes) {
        node.Update();
      }
    }
  }
}