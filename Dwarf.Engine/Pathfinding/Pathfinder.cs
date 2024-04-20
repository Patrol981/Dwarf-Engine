using System.Numerics;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Utils;

namespace Dwarf.Engine.Pathfinding.AStar;

public class Pathfinder : DwarfScript {
  private Grid? _grid;

  public override void Awake() {
    _grid = Owner!.GetComponent<Grid>();
  }

  public void FindPath(Vector3 start, Vector3 end) {
    if (_grid == null) return;

    var startNode = _grid.NodeFromWorldPoint(start);
    var endNode = _grid.NodeFromWorldPoint(end);

    Heap<Node> openSet = new Heap<Node>(_grid.MaxSize);
    HashSet<Node> closedSet = new HashSet<Node>();

    openSet.Add(startNode);
    while (openSet.Count > 0) {
      var currentNode = openSet.RemoveFirst();
      closedSet.Add(currentNode);

      if (currentNode == endNode) {
        RetracePath(startNode, endNode);
        return;
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

  private void RetracePath(Node startNode, Node endNode) {
    var path = new List<Node>();
    var currentNode = endNode;

    while (currentNode != startNode) {
      path.Add(currentNode);
      currentNode = currentNode.Parent;
    }
    path.Reverse();

    _grid!.Path = path;
  }

  private int GetDistance(Node a, Node b) {
    var distX = MathF.Abs(a.GridPosition.X - b.GridPosition.X);
    var distY = MathF.Abs(a.GridPosition.Y - b.GridPosition.Y);

    return distX > distY ? (int)(14 * distY + 10 * (distX - distY)) : (int)(14 * distX + 10 * (distY - distX));
  }
}