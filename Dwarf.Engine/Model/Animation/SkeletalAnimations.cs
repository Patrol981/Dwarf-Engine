namespace Dwarf.Model.Animation;

public class skeletalAnimations {
  private Dictionary<string, SkeletalAnimation> _animations = [];
  private SkeletalAnimation _currentAnimation = null!;
  private int _frameCounter = 0;

  public static float TimeSave = 0.0f;

  public void Start(string animName) {
    var current = _animations[animName];
    if (current != null) {
      _currentAnimation = current;
      _currentAnimation.Start();
    }
  }

  public void Stop() {
    _currentAnimation?.Stop();
  }

  public void SetRepeat(bool repeat) {
    _currentAnimation?.SetRepeat(repeat);
  }

  public void SetRepeatAll(bool repeat) {
    var animations = _animations.Values.ToArray();
    for (int i = 0; i < animations.Length; i++) {
      animations[i].SetRepeat(repeat);
    }
  }

  public void Update(float timestep, ref Skeleton skeleton, int frameCounter) {
    if (_frameCounter != frameCounter) {
      _frameCounter = frameCounter;

      _currentAnimation?.Update(timestep, ref skeleton);
    }
  }

  public void AddAnimation(SkeletalAnimation skeletalAnimation) {
    _animations.TryAdd(skeletalAnimation.Name, skeletalAnimation);
  }

  public SkeletalAnimation GetSkeletalAnimation(string name) {
    _animations.TryGetValue(name, out var skeletalAnimation);
    if (skeletalAnimation != null) {
      return skeletalAnimation;
    } else {
      throw new ArgumentNullException(nameof(name));
    }
  }

  public bool IsRunning() {
    return _currentAnimation != null && _currentAnimation.IsRunning();
  }

  public bool WillExpire(float timestep) {
    return _currentAnimation != null && _currentAnimation.WillExpire(timestep);
  }

  public SkeletalAnimation Current => _currentAnimation;

  public int GetIndex(string animation) {
    bool found = false;
    foreach (var element in _animations.Values) {
      if (element.Name == animation) {
        found = true;
        break;
      }
    }
    if (found) {
      return 1;
    }
    return -1;
  }
}