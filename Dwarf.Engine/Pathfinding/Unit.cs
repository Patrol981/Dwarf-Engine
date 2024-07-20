using System.Collections;
using System.Numerics;
using Dwarf.Coroutines;
using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Pathfinding.AStar;
using Dwarf.Extensions.Logging;

namespace Dwarf.Pathfinding;
public class Unit : DwarfScript {
  private float _speed = .05f;
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
    if (pathSuccess && !IsMoving) {
      _path = newPath;
      _targetIndex = 0;
      IsMoving = true;
      await CoroutineRunner.Instance.StopCoroutine(FollowPath());
      CoroutineRunner.Instance.StartCoroutine(FollowPath());
    }
  }

  private IEnumerator FollowPath() {
    if (_path == null || _path.Length == 0) {
      IsMoving = false;
      yield break;
    }

    int currentWaypointIndex = 0;
    Vector3 currentWaypoint = _path[currentWaypointIndex];

    while (true) {
      if (Vector3.Distance(_transform.Position, currentWaypoint) < 0.01f) { // Use a tolerance value for position comparison
        currentWaypointIndex++;
        if (currentWaypointIndex >= _path.Length) {
          _path = null!;
          IsMoving = false;
          yield break;
        }
        currentWaypoint = _path[currentWaypointIndex];
        _transform.LookAtFixed(currentWaypoint);
      }
      _transform.Position = Transform.MoveTowards(_transform.Position, currentWaypoint, _speed * Time.DeltaTime);
      yield return null;
    }
  }

  private IEnumerator FollowPath_Old() {
    if (_path == null) { IsMoving = false; yield break; }
    if (_path!.Length <= 0) { IsMoving = false; yield break; }

    var currentWaypoint = _path[0];
    _transform.LookAtFixed(currentWaypoint);

    if (currentWaypoint == _transform.Position) {
      IsMoving = false;
      yield break;
    }

    while (true) {
      if (_transform.Position == currentWaypoint) {
        _targetIndex += 1;
        if (_targetIndex >= _path.Length) {
          _path = null!;
          IsMoving = false;
          yield break;
        }
        currentWaypoint = _path[_targetIndex];
        _transform.LookAtFixed(currentWaypoint);
      }
      _transform.Position = Transform.MoveTowards(_transform.Position, currentWaypoint, _speed * Time.DeltaTime);
      yield return null;
    }
  }

  public bool IsMoving { get; private set; } = false;
}

