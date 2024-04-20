using System.Numerics;

namespace Dwarf.Engine.Pathfinding.AStar;

public sealed class PathRequestManager {
  private readonly Queue<PathRequest> _pathRequestQueue = new Queue<PathRequest>();
  private PathRequest _currentPathRequest;

  public static void RequestPath(Vector3 pathStart, Vector3 pathEnd, Action<Vector3[], bool> callback) {
    var newRequest = new PathRequest(pathStart, pathEnd, callback);
    Instance._pathRequestQueue.Enqueue(newRequest);
    Instance.TryProcessNext();
  }

  private void TryProcessNext() {

  }

  public static PathRequestManager Instance { get; } = new();

  internal struct PathRequest(Vector3 start, Vector3 end, Action<Vector3[], bool> callback) {
    public Vector3 PathStart = start;
    public Vector3 PathEnd = end;
    public Action<Vector3[], bool> Callback = callback;
  }
}