using System.Collections;
using System.Numerics;
using Dwarf.Engine.Coroutines;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Utils;

namespace Dwarf.Engine.Pathfinding.AStar;

public class Pathfinder : DwarfScript {
  private Grid _grid = null!;

  public static Entity Create(Vector2 WorldSize = default) {
    var pathfindingManager = new Entity() {
      Name = "PathfindingManager"
    };
    pathfindingManager.AddTransform();
    pathfindingManager.AddComponent(new Grid());
    pathfindingManager.AddComponent(new Pathfinder());
    pathfindingManager.AddComponent(new PathRequestManager());
    if (WorldSize != default) {
      pathfindingManager.GetComponent<Grid>().GridSizeWorld = WorldSize;
    }
    return pathfindingManager;
  }

  public override void Awake() {
    _grid = Owner!.GetComponent<Grid>();
  }

  public void StartFindPath(Vector3 start, Vector3 end) {
    // CoroutineRunner.Instance.StartCoroutine(FindPath(start, end));
    FindPath(start, end);
  }

  public void FindPath(Vector3 start, Vector3 end) {
    var waypoints = new Vector3[0];
    var pathSuccess = false;

    var startNode = _grid!.NodeFromWorldPoint(start);
    var endNode = _grid.NodeFromWorldPoint(end);

    if (startNode.Walkable && endNode.Walkable) {
      Heap<Node> openSet = new Heap<Node>(_grid.MaxSize);
      HashSet<Node> closedSet = new HashSet<Node>();

      openSet.Add(startNode);
      while (openSet.Count > 0) {
        var currentNode = openSet.RemoveFirst();
        closedSet.Add(currentNode);

        if (currentNode == endNode) {
          pathSuccess = true;
          break;
        }

        foreach (var neighbourNode in _grid.GetNeighbours(currentNode)) {
          if (!neighbourNode.Walkable || closedSet.Contains(neighbourNode)) {
            continue;
          }

          var newCost = currentNode.GCost + GetDistance(currentNode, neighbourNode);
          if (newCost < neighbourNode.GCost || !openSet.Contains(neighbourNode)) {
            neighbourNode.GCost = newCost;
            neighbourNode.HCost = GetDistance(neighbourNode, endNode);
            neighbourNode.Parent = currentNode;

            if (!openSet.Contains(neighbourNode)) openSet.Add(neighbourNode);
          }
        }
      }
    }
    if (pathSuccess) {
      waypoints = RetracePath(startNode, endNode);
    }
    PathRequestManager.Instance.FinishedProcessingPath(waypoints, pathSuccess);
  }

  private Vector3[] RetracePath(Node startNode, Node endNode) {
    var path = new List<Node>();
    var currentNode = endNode;

    while (currentNode != startNode) {
      path.Add(currentNode);
      currentNode = currentNode.Parent;
    }
    var waypoints = SimplifyPath(path.ToArray());
    // var waypoints = ConvertPath(path.ToArray());
    Array.Reverse(waypoints);
    return waypoints;
  }

  private Vector3[] SimplifyPath(ReadOnlySpan<Node> path) {
    IList<Vector3> waypoints = [];
    var oldDir = Vector2.Zero;

    for (int i = 1; i < path.Length; i++) {
      var newDir = new Vector2(
        path[i - 1].GridPosition.X - path[i].GridPosition.X,
        path[i - 1].GridPosition.Y - path[i].GridPosition.Y
      );
      if (newDir != oldDir) {
        waypoints.Add(path[i].WorldPosition);
      }
      oldDir = newDir;
    }
    return [.. waypoints];
  }

  private Vector3[] ConvertPath(ReadOnlySpan<Node> path) {
    IList<Vector3> waypoints = [];
    foreach (var p in path) {
      waypoints.Add(p.WorldPosition);
    }
    return [.. waypoints];
  }

  private int GetDistance(Node a, Node b) {
    var distX = MathF.Abs(a.GridPosition.X - b.GridPosition.X);
    var distY = MathF.Abs(a.GridPosition.Y - b.GridPosition.Y);

    return distX > distY ? (int)(14 * distY + 10 * (distX - distY)) : (int)(14 * distX + 10 * (distY - distX));
  }
}