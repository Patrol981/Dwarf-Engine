using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;

namespace Dwarf.Procedural.Cave;

public class CaveGenerator {
  public const int FillPercent = 43;
  public const int SmoothIterationCount = 5;
  public int Width { get; init; } = 128;
  public int Height { get; init; } = 128;
  public string Seed { get; init; } = default!;
  public int[,] Map { get; private set; }

  internal class Node {
    internal Vector3 Position;
    internal int VertexIndex = -1;

    internal Node(Vector3 pos) {
      Position = pos;
    }
  }

  internal class ControlNode : Node {
    internal bool Active;
    internal Node Above;
    internal Node Right;

    internal ControlNode(Vector3 pos, bool active, float squareSize) : base(pos) {
      Active = active;
      Above = new(Position + Vector3.UnitZ * squareSize / 2f);
      Right = new(Position + Vector3.UnitX * squareSize / 2f);
    }
  }

  internal class Square {
    internal ControlNode TopLeft;
    internal ControlNode TopRight;
    internal ControlNode BottomRight;
    internal ControlNode BottomLeft;
    internal Node CenterTop;
    internal Node CenterRight;
    internal Node CenterBottom;
    internal Node CenterLeft;

    internal int Configuration;

    internal Square(
      ControlNode topLeft, ControlNode topRight,
      ControlNode bottomRight, ControlNode bottomLeft
    ) {
      TopLeft = topLeft;
      TopRight = topRight;
      BottomRight = bottomRight;
      BottomLeft = bottomLeft;

      CenterTop = TopLeft.Right;
      CenterRight = BottomRight.Above;
      CenterBottom = BottomLeft.Right;
      CenterLeft = BottomLeft.Above;

      if (TopLeft.Active) Configuration += 8;
      if (TopRight.Active) Configuration += 4;
      if (BottomRight.Active) Configuration += 2;
      if (BottomLeft.Active) Configuration += 1;
    }
  }

  internal class SquareGrid {
    internal Square[,] Squares;
    internal List<Vector3> Vertices;
    internal List<uint> Triangles;

    internal SquareGrid(int[,] map, float squareSize) {
      int nodeCountX = map.GetLength(0);
      int nodeCountY = map.GetLength(1);
      float mapWidth = nodeCountX * squareSize;
      float mapHeight = nodeCountY * squareSize;

      var controlNodes = new ControlNode[nodeCountX, nodeCountY];

      for (int x = 0; x < nodeCountX; x++) {
        for (int y = 0; y < nodeCountY; y++) {
          var pos = new Vector3(
            -mapWidth / 2 + x * squareSize + squareSize / 2,
            0,
            -mapHeight / 2 + y * squareSize + squareSize / 2
          );
          controlNodes[x, y] = new(pos, map[x, y] == 1, squareSize);
        }
      }

      Squares = new Square[nodeCountX - 1, nodeCountY - 1];
      for (int x = 0; x < nodeCountX - 1; x++) {
        for (int y = 0; y < nodeCountY - 1; y++) {
          Squares[x, y] = new Square(
            controlNodes[x, y + 1],
            controlNodes[x + 1, y + 1],
            controlNodes[x + 1, y],
            controlNodes[x, y]
          );
        }
      }

      Vertices = [];
      Triangles = [];
    }

    internal void GenerateMesh() {
      for (int x = 0; x < Squares.GetLength(0); x++) {
        for (int y = 0; y < Squares.GetLength(1); y++) {
          TriangulateSquare(Squares[x, y]);
        }
      }
    }

    internal void TriangulateSquare(Square square) {
      switch (square.Configuration) {
        case 0:
          break;

        // 1 point cases
        case 1:
          MeshFromPoints(square.CenterBottom, square.BottomLeft, square.CenterLeft);
          break;
        case 2:
          MeshFromPoints(square.CenterRight, square.BottomRight, square.CenterBottom);
          break;
        case 4:
          MeshFromPoints(square.CenterTop, square.TopRight, square.CenterRight);
          break;
        case 8:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterLeft);
          break;

        // 2 point cases
        case 3:
          MeshFromPoints(square.CenterRight, square.BottomRight, square.BottomLeft, square.CenterLeft);
          break;
        case 6:
          MeshFromPoints(square.CenterTop, square.TopRight, square.BottomRight, square.CenterBottom);
          break;
        case 9:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterBottom, square.BottomLeft);
          break;
        case 12:
          MeshFromPoints(square.TopLeft, square.TopRight, square.CenterRight, square.CenterLeft);
          break;
        case 5:
          MeshFromPoints(square.CenterTop, square.TopRight, square.CenterRight, square.CenterBottom, square.BottomLeft, square.CenterLeft);
          break;
        case 10:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterRight, square.BottomRight, square.CenterBottom, square.CenterLeft);
          break;

        // 3 point cases
        case 7:
          MeshFromPoints(square.CenterTop, square.TopRight, square.BottomRight, square.BottomLeft, square.CenterLeft);
          break;
        case 11:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterRight, square.BottomRight, square.BottomLeft);
          break;
        case 13:
          MeshFromPoints(square.TopLeft, square.TopRight, square.CenterRight, square.CenterBottom, square.BottomLeft);
          break;
        case 14:
          MeshFromPoints(square.TopLeft, square.TopRight, square.BottomRight, square.CenterBottom, square.CenterLeft);
          break;

        // 4 point case
        case 15:
          MeshFromPoints(square.TopLeft, square.TopRight, square.BottomRight, square.BottomLeft);
          break;
      }
    }

    internal void MeshFromPoints(params Node[] points) {
      AssignVertices(points);

      if (points.Length >= 3) CreateTriangle(points[0], points[1], points[2]);
      if (points.Length >= 4) CreateTriangle(points[0], points[2], points[3]);
      if (points.Length >= 5) CreateTriangle(points[0], points[3], points[4]);
      if (points.Length >= 6) CreateTriangle(points[0], points[4], points[5]);
    }

    internal void AssignVertices(Node[] points) {
      for (int i = 0; i < points.Length; i++) {
        if (points[i].VertexIndex == -1) {
          points[i].VertexIndex = Vertices.Count;
          Vertices.Add(points[i].Position);
        }
      }
    }

    internal void CreateTriangle(Node a, Node b, Node c) {
      Triangles.AddRange([(uint)a.VertexIndex, (uint)b.VertexIndex, (uint)c.VertexIndex]);
    }
  }

  public CaveGenerator(string seed = default!) {
    Seed = seed;
    if (string.IsNullOrEmpty(Seed)) {
      Seed = Time.CurrentTime.ToString();
    }
    Map = new int[Width, Height];
  }

  public void GenerateMap(out Mesh mesh) {
    RandomFillMap();
    for (int i = 0; i < SmoothIterationCount; i++) {
      SmoothMap();
    }

    GenerateMesh(out mesh);
  }

  private void SmoothMap() {
    for (int x = 0; x < Width; x++) {
      for (int y = 0; y < Height; y++) {
        var neighbourWallTiles = GetSurroundingWallCount(x, y);

        if (neighbourWallTiles > 4) {
          Map[x, y] = 1;
        } else if (neighbourWallTiles < 4) {
          Map[x, y] = 0;
        }
      }
    }
  }

  private int GetSurroundingWallCount(int gX, int gY) {
    int wallCount = 0;

    for (int neighbourX = gX - 1; neighbourX <= gX + 1; neighbourX++) {
      for (int neighbourY = gY - 1; neighbourY <= gY + 1; neighbourY++) {
        if (neighbourX >= 0 && neighbourX < Width && neighbourY >= 0 && neighbourY < Height) {
          if (neighbourX != gX || neighbourY != gY) {
            wallCount += Map[neighbourX, neighbourY];
          }
        } else {
          wallCount++;
        }
      }
    }

    return wallCount;
  }

  private void RandomFillMap() {
    var prng = new System.Random(Seed.GetHashCode());

    for (int x = 0; x < Width; x++) {
      for (int y = 0; y < Height; y++) {
        if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1) {
          Map[x, y] = 1;
        } else {
          Map[x, y] = (prng.Next(0, 100) < FillPercent) ? 1 : 0;
        }
      }
    }
  }

  private void GenerateMesh(out Mesh mesh) {
    var grid = new SquareGrid(Map, 20);
    grid.GenerateMesh();

    mesh = new Mesh(Application.Instance.Device, Matrix4x4.Identity) {
      Vertices = grid.Vertices.Select(x => {
        return new Vertex() {
          Position = x,
          Color = new(0.2f, 0.6f, 0.2f),
          Normal = Vector3.UnitY,
        };
      }).ToArray(),
      VertexCount = (ulong)grid.Vertices.Count,
      Indices = [.. grid.Triangles],
      IndexCount = (ulong)grid.Triangles.Count
    };
  }
}