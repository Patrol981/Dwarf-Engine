using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;

namespace Dwarf.Model.Animation;

public class AnimationController : Component {
  private MeshRenderer _meshRenderer;
  private readonly Dictionary<string, Animation> _animations = [];
  private Animation _currentAnimation = null!;

  public AnimationController() {
    var mr = Owner?.TryGetComponent<MeshRenderer>();
    if (mr != null) {
      _meshRenderer = mr;
    } else {
      _meshRenderer = new MeshRenderer();
    }
  }
  public void Init(MeshRenderer meshRenderer) {
    _meshRenderer = meshRenderer;

    foreach (var animation in meshRenderer.Animations) {
      _animations.TryAdd(animation.Name, animation);
    }
  }

  public void PlayFirstAnimation() {
    if (_animations.Count < 1) return;
    _currentAnimation = _animations.First().Value;
  }

  public void SetCurrentAnimation(string animationName) {
    _animations.TryGetValue(animationName, out var animation);
    if (animation == null) {
      Logger.Error($"Animation {animationName} is not found.");
      return;
    }

    _currentAnimation = animation;
  }
  public void Update(Node node) {
    if (_currentAnimation == null) return;

    node.AnimationTimer += 0.015f;
    if (node.AnimationTimer > _currentAnimation.End) {
      node.AnimationTimer -= _currentAnimation.End;
    }

    UpdateAnimation(_currentAnimation, node.AnimationTimer);
  }

  public void UpdateAnimation(Animation animation, float time) {
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
                sampler.Translate(i, time, channel.Node);
                break;
              case AnimationChannel.PathType.Rotation:
                sampler.Rotate(i, time, channel.Node);
                break;
              case AnimationChannel.PathType.Scale:
                sampler.Scale(i, time, channel.Node);
                break;
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