using System.Numerics;

using Dwarf.Engine.Math;

namespace Dwarf.Engine.Procedural;
public class Terrain3D : MeshRenderer {
  const int HEIGHT = 512;
  const int WIDTH = 512;

  private double[,] _points;
  private readonly Application _app;

  private uint _size = 1024;

  public Terrain3D() { }

  public Terrain3D(Application app) : base(app.Device, app.Renderer) {
    _app = app;
    _points = new double[HEIGHT, WIDTH];
  }

  public void Setup() {
    var mesh = Generate(_app);
    base.Init(new Mesh[] { mesh });
    SetupTexture(_app);
    base.BindToTexture(_app.TextureManager, Owner!.EntityID.ToString());
  }

  private Mesh Generate(Application app) {
    var rand = new Random();

    for (int y = 0; y < HEIGHT; y++) {
      for (int x = 0; x < WIDTH; x++) {
        double nx = x / WIDTH - 0.5;
        double ny = y / HEIGHT - 0.5;
        _points[x, y] = Noise.Perlin((float)nx, (float)ny);
      }
    }

    var verts = new List<Vertex>();
    var mesh = new Mesh();

    for (int y = 0; y < HEIGHT; y++) {
      for (int x = 0; x < WIDTH; x++) {
        var v = new Vertex();
        v.Position = new Vector3(
          (float)x / ((float)WIDTH - 1) * _size,
          0,
          // (float)_points[x, y] * 1.1f,
          (float)y / ((float)HEIGHT - 1) * _size
        );

        v.Normal = new Vector3(
          0,
          1,
          0
        );

        v.Uv = new Vector2(
          (float)x / ((float)WIDTH - 1),
          (float)y / ((float)HEIGHT - 1)
        );


        v.Color = new Vector3(
          rand.NextSingle(),
          rand.NextSingle(),
          rand.NextSingle()
        );

        /*
        v.Color = new Vector3(
          1,
          1,
          1
        );
        */

        verts.Add(v);
      }
    }

    mesh.Vertices = verts.ToArray();
    var indices = new List<uint>();

    for (int gz = 0; gz < HEIGHT - 1; gz++) {
      for (int gx = 0; gx < WIDTH - 1; gx++) {
        uint topLeft = (uint)((gz * WIDTH) + gx);
        uint topRight = topLeft + 1;
        uint bottomLeft = (uint)(((gz + 1) * WIDTH) + gx);
        uint bottomRight = bottomLeft + 1;

        indices.Add((uint)bottomLeft);
        indices.Add((uint)topLeft);

        indices.Add((uint)topRight);
        indices.Add((uint)topRight);

        indices.Add((uint)bottomRight);
        indices.Add((uint)bottomLeft);

        /*
        indices.Add((uint)topLeft);
        indices.Add((uint)bottomLeft);
        indices.Add((uint)topRight);
        indices.Add((uint)topRight);
        indices.Add((uint)bottomLeft);
        indices.Add((uint)bottomRight);
        */
      }
    }

    mesh.Indices = indices.ToArray();
    // mesh = Primitives.CreateCylinderPrimitive(0.5f, 1.5f, 20);

    return mesh;
  }

  private async void SetupTexture(Application app) {
    var data = await TextureLoader.LoadDataFromPath("./Resources/Textures/base/no_texture.png");
    var texture = new VulkanTexture(app.Device, data.Width, data.Height, Owner!.EntityID.ToString());
    texture.SetTextureData(data.Data);
    await app.TextureManager.AddTexture(texture);
  }
}
