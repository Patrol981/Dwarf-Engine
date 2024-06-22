using System.Numerics;
using System.Runtime.InteropServices;

using Dwarf.EntityComponentSystem;

namespace Dwarf;

/*
[StructLayout(LayoutKind.Explicit)]
public struct MaterialData {
  [FieldOffset(0)] public Vector3 Color;
  [FieldOffset(16)] public Vector3 Ambient;
  [FieldOffset(32)] public Vector3 Diffuse;
  [FieldOffset(48)] public Vector3 Specular;
  [FieldOffset(60)] public float Shininess;
}
*/

[StructLayout(LayoutKind.Sequential)]
public struct MaterialData {
  public Vector3 Color;
  public Vector3 Ambient;
  public Vector3 Diffuse;
  public Vector3 Specular;
  public float Shininess;
}

public class Material : Component {
  private MaterialData _materialData;

  public Material(Vector3 color) {
    Init();

    _materialData.Color = color;
  }

  public Material() {
    Init();
  }

  private void Init() {
    _materialData = new() {
      Color = new(1, 1, 1),
      Shininess = 1.0f,
      Ambient = new(1.0f, 1.0f, 1.0f),
      Diffuse = new(0.0f, 0.0f, 0.0f),
      Specular = new(0, 0, 0)
    };
  }

  public Vector3 Color {
    get { return _materialData.Color; }
    set { _materialData.Color = value; }
  }

  public Vector3 Ambient {
    get { return _materialData.Ambient; }
    set { _materialData.Ambient = value; }
  }

  public Vector3 Diffuse {
    get { return _materialData.Diffuse; }
    set { _materialData.Diffuse = value; }
  }

  public Vector3 Specular {
    get { return _materialData.Specular; }
    set { _materialData.Specular = value; }
  }

  public float Shininess {
    get { return _materialData.Shininess; }
    set { _materialData.Shininess = value; }
  }

  public MaterialData Data => _materialData;
}