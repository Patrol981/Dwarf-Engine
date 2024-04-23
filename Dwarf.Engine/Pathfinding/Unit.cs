using System.Collections;
using System.Numerics;
using Dwarf.Engine.Coroutines;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Pathfinding.AStar;
using Dwarf.Extensions.Logging;

namespace Dwarf.Engine.Pathfinding;
public class Unit : DwarfScript {
  private float _speed = .1f;
  private Vector3[] _path = [];
  private int _targetIndex;
  private Transform _transform = null!;

  public override void Awake() {
    var hasTransform = Owner!.HasComponent<Transform>();
    if (!hasTransform) {
      Owner!.AddComponent(new Transform());
    }
    _transform = Owner!.GetComponent<Transform>();
  }

  public override void Start() {
    // PathRequestManager.RequestPath(Owner!.GetComponent<Transform>().Position, Target.Position, OnPathFound);
  }

  public override void Update() {
  }

  public async void OnPathFound(Vector3[] newPath, bool pathSuccess) {
    if (pathSuccess) {
      _path = newPath;
      _targetIndex = 0;
      await CoroutineRunner.Instance.StopCoroutine(FollowPath());
      CoroutineRunner.Instance.StartCoroutine(FollowPath());
    }
  }

  private IEnumerator FollowPath() {
    if (_path == null) yield break;
    if (_path!.Length <= 0) yield break;

    var currentWaypoint = _path[0];

    if (currentWaypoint == _transform.Position) yield break;

    while (true) {
      if (_transform.Position == currentWaypoint) {
        _targetIndex += 1;
        if (_targetIndex >= _path.Length) {
          _path = null!;
          yield break;
        }
        currentWaypoint = _path[_targetIndex];
      }
      _transform.Position = Transform.MoveTowards(_transform.Position, currentWaypoint, _speed * Time.DeltaTime);
      // yield return new WaitForSeconds(.1f);
      yield return null;
    }
  }
}

