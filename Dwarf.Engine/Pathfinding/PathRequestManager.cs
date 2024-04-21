using System.Numerics;
using Dwarf.Engine.EntityComponentSystem;

namespace Dwarf.Engine.Pathfinding.AStar;

public class PathRequestManager : DwarfScript {
  private readonly Queue<PathRequest> _pathRequestQueue = new Queue<PathRequest>();
  private PathRequest _currentPathRequest;
  private bool _isProcessingPath = false;

  private Pathfinder _pathfinder = null!;

  public override void Awake() {
    _pathfinder = Owner!.GetComponent<Pathfinder>()!;
  }

  public static void RequestPath(Vector3 pathStart, Vector3 pathEnd, Action<Vector3[], bool> callback) {
    var newRequest = new PathRequest(pathStart, pathEnd, callback);
    Instance._pathRequestQueue.Enqueue(newRequest);
    Instance.TryProcessNext();
  }

  public void FinishedProcessingPath(Vector3[] path, bool success) {
    _currentPathRequest.Callback(path, success);
    _isProcessingPath = false;
    TryProcessNext();
  }

  private void TryProcessNext() {
    if (_isProcessingPath && _pathRequestQueue.Count < 1) return;

    _currentPathRequest = _pathRequestQueue.Dequeue();
    _isProcessingPath = true;
    _pathfinder.StartFindPath(_currentPathRequest.PathStart, _currentPathRequest.PathEnd);
  }

  public static PathRequestManager Instance { get; } = new();

  internal struct PathRequest(Vector3 start, Vector3 end, Action<Vector3[], bool> callback) {
    public Vector3 PathStart = start;
    public Vector3 PathEnd = end;
    public Action<Vector3[], bool> Callback = callback;
  }
}