using System.Numerics;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;

namespace Dwarf.Procedural.Cave;

public class CaveGenerator {
  public int FillPercent { get; private set; }
  public int SmoothIterationCount { get; private set; }
  public int BorderSize { get; private set; }
  public int WallHeight { get; private set; }
  public int Width { get; init; }
  public int Height { get; init; }
  public string Seed { get; init; }
  public int[,] Map { get; private set; }

  public class GeneratorOptions {
    public int FillPercent = 43;
    public int SmoothIterationCount = 5;
    public int BorderSize = 5;
    public int WallHeight = 50;
    public int MapWidth = 64;
    public int MapHeight = 64;
    public string Seed = default!;
  }

  internal struct Coord {
    internal int TileX;
    internal int TileY;

    internal Coord(int x, int y) {
      TileX = x;
      TileY = y;
    }
  }

  internal struct Triangle {
    internal int VertexIndexA;
    internal int VertexIndexB;
    internal int VertexIndexC;
    internal int[] Vertices;

    internal Triangle(int a, int b, int c) {
      VertexIndexA = a;
      VertexIndexB = b;
      VertexIndexC = c;

      Vertices = new int[3];
      Vertices[0] = VertexIndexA;
      Vertices[1] = VertexIndexB;
      Vertices[2] = VertexIndexC;
    }

    internal int this[int i] {
      get {
        return Vertices[i];
      }
    }

    internal bool Contains(int vertexIndex) {
      return vertexIndex == VertexIndexA || vertexIndex == VertexIndexB || vertexIndex == VertexIndexC;
    }
  }

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
    internal Dictionary<int, List<Triangle>> TriangleDictionary;
    internal List<List<int>> Outlines;
    internal HashSet<int> CheckedVertices;

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
      TriangleDictionary = [];
      Outlines = [];
      CheckedVertices = [];
    }

    internal void GenerateMesh() {
      Outlines.Clear();
      CheckedVertices.Clear();
      TriangleDictionary.Clear();

      for (int x = 0; x < Squares.GetLength(0); x++) {
        for (int y = 0; y < Squares.GetLength(1); y++) {
          TriangulateSquare(Squares[x, y]);
        }
      }
    }

    internal void GenerateWallMesh(IDevice device, int wallHeight, out Mesh wallMesh) {
      CalculateMeshOutlines();

      var wallVertices = new List<Vector3>();
      var wallTriangles = new List<uint>();
      wallMesh = new Mesh(device, Matrix4x4.Identity);

      foreach (var outline in Outlines) {
        for (int i = 0; i < outline.Count - 1; i++) {
          var startIndex = wallVertices.Count;
          wallVertices.Add(Vertices[outline[i]]); // left vertex
          wallVertices.Add(Vertices[outline[i + 1]]); // right vertex
          wallVertices.Add(Vertices[outline[i]] - (Vector3.UnitY * wallHeight)); // bottom left vertex
          wallVertices.Add(Vertices[outline[i + 1]] - (Vector3.UnitY * wallHeight)); // bottom right vertex

          wallTriangles.Add((uint)startIndex + 0);
          wallTriangles.Add((uint)startIndex + 2);
          wallTriangles.Add((uint)startIndex + 3);

          wallTriangles.Add((uint)startIndex + 3);
          wallTriangles.Add((uint)startIndex + 1);
          wallTriangles.Add((uint)startIndex + 0);
        }
      }

      wallMesh.Vertices = wallVertices.Select(x => {
        return new Vertex() {
          Position = x,
          Color = new(1.0f, 1.0f, 1.0f),
          Normal = Vector3.UnitY,
        };
      }).ToArray();
      wallMesh.VertexCount = (ulong)wallVertices.Count;
      wallMesh.Indices = [.. wallTriangles];
      wallMesh.IndexCount = (ulong)wallTriangles.Count;
    }

    internal void TriangulateSquare(Square square) {
      switch (square.Configuration) {
        case 0:
          break;

        // 1 point cases
        case 1:
          MeshFromPoints(square.CenterLeft, square.CenterBottom, square.BottomLeft);
          break;
        case 2:
          MeshFromPoints(square.BottomRight, square.CenterBottom, square.CenterRight);
          break;
        case 4:
          MeshFromPoints(square.TopRight, square.CenterRight, square.CenterTop);
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
          CheckedVertices.Add(square.TopLeft.VertexIndex);
          CheckedVertices.Add(square.TopRight.VertexIndex);
          CheckedVertices.Add(square.BottomRight.VertexIndex);
          CheckedVertices.Add(square.BottomLeft.VertexIndex);
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

      var triangle = new Triangle(a.VertexIndex, b.VertexIndex, c.VertexIndex);
      AddTriangleToDictionary(triangle.VertexIndexA, triangle);
      AddTriangleToDictionary(triangle.VertexIndexB, triangle);
      AddTriangleToDictionary(triangle.VertexIndexC, triangle);
    }

    internal void AddTriangleToDictionary(int vertexIndexKey, Triangle triangle) {
      if (TriangleDictionary.ContainsKey(vertexIndexKey)) {
        TriangleDictionary[vertexIndexKey].Add(triangle);
      } else {
        var triangleList = new List<Triangle> {
          triangle
        };
        TriangleDictionary.Add(vertexIndexKey, triangleList);
      }
    }

    internal void CalculateMeshOutlines() {
      for (int vertexIndex = 0; vertexIndex < Vertices.Count; vertexIndex++) {
        if (!CheckedVertices.Contains(vertexIndex)) {
          var newOutlineVertex = GetconnectedOutlineVertex(vertexIndex);
          if (newOutlineVertex != -1) {
            CheckedVertices.Add(vertexIndex);

            var newOutline = new List<int> {
              vertexIndex
            };

            Outlines.Add(newOutline);
            FollowOutline(newOutlineVertex, Outlines.Count - 1);
            Outlines[^1].Add(vertexIndex);
          }
        }
      }
    }

    internal void FollowOutline(int vertexIndex, int outlineIndex) {
      Outlines[outlineIndex].Add(vertexIndex);
      CheckedVertices.Add(vertexIndex);
      var nextVertexIndex = GetconnectedOutlineVertex(vertexIndex);

      if (nextVertexIndex != -1) {
        FollowOutline(nextVertexIndex, outlineIndex);
      }
    }

    internal bool IsOutlineEdge(int vertexA, int vertexB) {
      var trianglesContainingVertexA = TriangleDictionary[vertexA];
      int sharedTriangleCount = 0;

      for (int i = 0; i < trianglesContainingVertexA.Count; i++) {
        if (trianglesContainingVertexA[i].Contains(vertexB)) {
          sharedTriangleCount++;
          if (sharedTriangleCount > 1) {
            break;
          }
        }
      }

      return sharedTriangleCount == 1;
    }

    internal int GetconnectedOutlineVertex(int vertexIndex) {
      var trianglesContainingVertex = TriangleDictionary[vertexIndex];

      for (int i = 0; i < trianglesContainingVertex.Count; i++) {
        var triangle = trianglesContainingVertex[i];
        for (int j = 0; j < 3; j++) {
          var vertexB = triangle[j];

          if (vertexB != vertexIndex && !CheckedVertices.Contains(vertexB)) {
            if (IsOutlineEdge(vertexIndex, vertexB)) {
              return vertexB;
            }
          }
        }
      }

      return -1;
    }
  }

  public CaveGenerator(Action<GeneratorOptions> options) {
    var config = new GeneratorOptions();
    options(config);

    Width = config.MapWidth;
    Height = config.MapHeight;
    FillPercent = config.FillPercent;
    SmoothIterationCount = config.SmoothIterationCount;
    BorderSize = config.BorderSize;
    WallHeight = config.WallHeight;

    Seed = config.Seed;
    if (string.IsNullOrEmpty(Seed)) {
      Seed = Time.CurrentTime.ToString();
    }
    Map = new int[Width, Height];
  }

  public void GenerateMap(IDevice device, out Mesh mesh, out Mesh wallMesh) {
    RandomFillMap();
    for (int i = 0; i < SmoothIterationCount; i++) {
      SmoothMap();
    }

    var borderedMap = new int[Width + BorderSize * 2, Height + BorderSize * 2];

    for (int x = 0; x < borderedMap.GetLength(0); x++) {
      for (int y = 0; y < borderedMap.GetLength(1); y++) {
        if (x >= BorderSize && x < Width + BorderSize && y >= BorderSize && y < Height + BorderSize) {
          borderedMap[x, y] = Map[x - BorderSize, y - BorderSize];
        } else {
          borderedMap[x, y] = 1;
        }
      }
    }

    GenerateMesh(device, borderedMap, out mesh, out wallMesh);
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

  private List<Coord> GetRegionTiles(int startX, int startY) {
    var tiles = new List<Coord>();
    var mapFlags = new int[Width, Height];

    return tiles;
  }

  private void GenerateMesh(IDevice device, int[,] map, out Mesh mesh, out Mesh wallMesh) {
    var grid = new SquareGrid(map, 20);
    grid.GenerateMesh();

    mesh = new Mesh(Application.Instance.Device, Matrix4x4.Identity) {
      Vertices = grid.Vertices.Select(x => {
        return new Vertex() {
          Position = x,
          Color = new(1.0f, 1.0f, 1.0f),
          Normal = Vector3.UnitY,
        };
      }).ToArray(),
      VertexCount = (ulong)grid.Vertices.Count,
      Indices = [.. grid.Triangles],
      IndexCount = (ulong)grid.Triangles.Count
    };

    grid.GenerateWallMesh(device, WallHeight, out wallMesh);
  }
}