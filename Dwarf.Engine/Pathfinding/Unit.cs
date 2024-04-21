using System.Numerics;
using Dwarf.Engine.Coroutines;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Pathfinding.AStar;

namespace Dwarf.Engine.Pathfinding;
public class Unit : DwarfScript {
  public Transform Target = null!;
  private float _speed = 5;
  private Vector3[] _path;
  private int _targetIndex;

  public override void Awake() {
    var hasTransform = Owner!.HasComponent<Transform>();
    if (!hasTransform) {
      Owner!.AddComponent(new Transform());
    }
  }

  public override void Start() {
    PathRequestManager.RequestPath(Owner!.GetComponent<Transform>().Position, Target.Position, OnPathFound);
  }

  public void OnPathFound(Vector3[] newPath, bool pathSuccess) {
    if (pathSuccess) {
      _path = newPath;
      // CoroutineRunner.Instance.StartCoroutine()
    }
  }
}

