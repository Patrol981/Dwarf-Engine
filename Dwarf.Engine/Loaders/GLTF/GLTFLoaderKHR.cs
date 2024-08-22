using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Utils;

using glTFLoader;
using glTFLoader.Schema;

namespace Dwarf.Loaders;

public static partial class GLTFLoaderKHR {
  public static async Task<MeshRenderer> LoadGLTF(Application app, string path, bool preload = false, int flip = 1) {
    var gltf = Interface.LoadModel(path);
    var glb = Interface.LoadBinaryBuffer(path);

    return await LoadGLTFNew(app, path, preload, flip);

    string[] paths;

    var nodeMaterialPair = new Dictionary<Dwarf.Model.Node, int>();

    foreach (var node in gltf.Nodes) {
      if (node.Mesh.HasValue) {
        ProcessMesh(app.Device, gltf, glb, node, ref nodeMaterialPair);
      }
    }
    ProcessMaterial(gltf, glb, nodeMaterialPair, path, out paths);
    ProcessSkeleton(gltf, glb, ref nodeMaterialPair);

    var textures = await TextureManager.AddTextures(app.Device, paths, flip);
    app.TextureManager.AddRange([.. textures]);

    var meshRenderer = new MeshRenderer(app.Device, app.Renderer, [.. nodeMaterialPair.Keys], [.. nodeMaterialPair.Keys]);

    if (meshRenderer.MeshedNodesCount == textures.Length || textures.Length > meshRenderer.MeshedNodesCount) {
      meshRenderer.BindMultipleModelPartsToTextures(app.TextureManager, paths);
    } else if (paths.Length > 0) {
      meshRenderer.BindMultipleModelPartsToTexture(app.TextureManager, paths[0]);
    } else {
      meshRenderer.BindMultipleModelPartsToTexture(app.TextureManager, "./Resources/Textures/base/no_texture.png");
    }

    // meshRenderer.Meshes[0].Skin.Skeleton.Traverse();

    return meshRenderer;
  }

  private static void ProcessSkeleton(
    Gltf gltf,
    byte[] globalBuffer,
    ref Dictionary<Dwarf.Model.Node, int> meshMaterialPair
  ) {
    foreach (var mesh in meshMaterialPair.Keys) {
      // mesh.Skin = new();
      // mesh.Skin.LoadAnimations(gltf, globalBuffer);
      // Skeleton skeleton = new(mesh.MeshNode);
      // mesh.Skin = new(skeleton, mesh.MeshNode);
      // mesh.Skin.Init(gltf, globalBuffer);
    }
  }

  private static void ProcessMaterial(
    Gltf gltf,
    byte[] globalBuffer,
    Dictionary<Dwarf.Model.Node, int> meshMaterialPair,
    string path,
    out string[] paths
  ) {
    string[] tags = GetFileNames(path, meshMaterialPair.Count);
    paths = new string[tags.Length];

    int i = 0;
    foreach (var value in meshMaterialPair.Values) {
      var material = gltf.Materials[value];

      if (material.PbrMetallicRoughness != null && material.PbrMetallicRoughness.BaseColorTexture != null) {
        var baseColorTexIdx = material.PbrMetallicRoughness.BaseColorTexture.Index;

        var texture = gltf.Textures[baseColorTexIdx];
        var imageIdx = texture.Source!.Value;
        var image = gltf.Images[imageIdx];

        var bufferView = gltf.BufferViews[image.BufferView!.Value];
        var buffer = gltf.Buffers[bufferView.Buffer];

        using var stream = new MemoryStream(globalBuffer, bufferView.ByteOffset, bufferView.ByteLength);
        using var reader = new BinaryReader(stream);

        byte[] imgData = reader.ReadBytes(bufferView.ByteLength);
        var targetPath = Path.Join(DwarfPath.AssemblyDirectory, $"./Resources/{tags[i]}.png");
        File.WriteAllBytes(targetPath, imgData);
        paths[i] = targetPath;

        i++;
      }
    }
  }

  private static string GetFileName(string path) {
    string filename = string.Empty;

    try {
      filename = Path.GetFileName(path);
    } catch {
      int length = (int)MathF.Min(5, path.Length);
      filename = path.Substring(path.Length - length, length);
    }

    return filename;
  }

  private static string[] GetFileNames(string path, int imagesCount) {
    var filename = GetFileName(path);
    var filenames = new string[imagesCount];

    for (int i = 0; i < imagesCount; i++) {
      filenames[i] = $"{filename}{i + 1}";
    }

    return filenames;
  }

  private static void ProcessMesh(
    IDevice device,
    Gltf gltf,
    byte[] globalBuffer,
    glTFLoader.Schema.Node mesh,
    ref Dictionary<Dwarf.Model.Node, int> meshMaterialPair
  ) {
    var vertices = new List<Vertex>();
    var nodeMatrices = new List<Matrix4x4>();
    var indices = new List<uint>();

    var materialId = -1;
    var targetMesh = gltf.Meshes[mesh.Mesh!.Value];

    foreach (var primitive in targetMesh.Primitives) {
      Vector2[] textureCoords = [];
      Vector3[] normals = [];
      Vector4[] weights = [];
      Vector4I[] joints = [];
      Vector3[] positions = [];

      if (primitive.Attributes.TryGetValue("TEXCOORD_0", out int coordIdx)) {
        LoadAccessor<float>(gltf, globalBuffer, gltf.Accessors[coordIdx], out var texFloats);
        textureCoords = texFloats.ToVector2Array();
      }
      if (primitive.Attributes.TryGetValue("POSITION", out int positionIdx)) {
        LoadAccessor<float>(gltf, globalBuffer, gltf.Accessors[positionIdx], out var posFloats);
        positions = posFloats.ToVector3Array();
      }
      if (primitive.Attributes.TryGetValue("NORMAL", out int normalIdx)) {
        LoadAccessor<float>(gltf, globalBuffer, gltf.Accessors[normalIdx], out var normFloats);
        normals = normFloats.ToVector3Array();
      }
      if (primitive.Attributes.TryGetValue("WEIGHTS_0", out int weightsIdx)) {
        LoadAccessor<float>(gltf, globalBuffer, gltf.Accessors[weightsIdx], out var weightFLoats);
        weights = weightFLoats.ToVector4Array();
      }
      if (primitive.Attributes.TryGetValue("JOINTS_0", out int jointsIdx)) {
        try {
          LoadAccessor<ushort>(gltf, globalBuffer, gltf.Accessors[jointsIdx], out var jointIndices);
          joints = jointIndices.ToVec4IArray();
        } catch {
          LoadAccessor<byte>(gltf, globalBuffer, gltf.Accessors[jointsIdx], out var jointIndices);
          joints = jointIndices.ToVec4IArray();
        }
      }
      if (primitive.Indices.HasValue) {
        var idx = primitive.Indices.Value;
        var idc = GetIndexAccessor(gltf, globalBuffer, idx);
        indices.AddRange(idc);
      }

      var vertex = new Vertex();
      var nodeMat = CreateMatrixFromNodeData(mesh);
      nodeMatrices.Add(nodeMat);
      for (int i = 0; i < positions.Length; i++) {
        // vertex.Position = Vector3.Transform(positions[i], nodeMat);
        vertex.Position = positions[i];
        // vertex.Position = positions[i] + mesh.Translation.ToVector3();
        vertex.Color = Vector3.One;
        vertex.Normal = normals.Length > 0 ? normals[i] : new Vector3(1, 1, 1);
        vertex.Uv = textureCoords.Length > 0 ? textureCoords[i] : new Vector2(0, 0);

        vertex.JointWeights = weights.Length > 0 ? weights[i] : new Vector4(0, 0, 0, 0);
        vertex.JointIndices = joints.Length > 0 ? joints[i] : new Vector4I(0, 0, 0, 0);

        vertices.Add(vertex);
      }

      var material = primitive.Material;
      if (!material.HasValue) continue;
      materialId = material.Value;
    }

    if (vertices.Count < 1) return;

    var meshData = new Mesh(device) {
      Vertices = [.. vertices],
      Indices = [.. indices],
    };

    var node = new Dwarf.Model.Node {
      Mesh = meshData,
      Translation = mesh.Translation.ToVector3(),
      Rotation = mesh.Rotation.ToQuat(),
      Scale = mesh.Scale.ToVector3(),
      NodeMatrix = mesh.Matrix.ToMatrix4x4(),
      Name = mesh.Name,
    };

    meshMaterialPair.Add(node, materialId);
  }

  public static Matrix4x4 CreateMatrixFromNodeData(glTFLoader.Schema.Node node) {
    var translation = node.Translation != null ? new Vector3(node.Translation[0], node.Translation[1], node.Translation[2]) : Vector3.Zero;
    var rotation = node.Rotation != null ? new Quaternion(node.Rotation[0], node.Rotation[1], node.Rotation[2], node.Rotation[3]) : Quaternion.Identity;
    var scale = node.Scale != null ? new Vector3(node.Scale[0], node.Scale[1], node.Scale[2]) : Vector3.One;

    var translationMatrix = Matrix4x4.CreateTranslation(translation);
    var rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
    var scaleMatrix = Matrix4x4.CreateScale(scale);

    // Combine the TRS matrices
    return scaleMatrix * rotationMatrix * translationMatrix;
  }

  public static Matrix4x4 GetNodeMatrix(glTFLoader.Schema.Node node) {
    if (node.Matrix != null && node.Matrix.Length == 16) {
      var mat1 = new Matrix4x4(
          node.Matrix[0], node.Matrix[1], node.Matrix[2], node.Matrix[3],
          node.Matrix[4], node.Matrix[5], node.Matrix[6], node.Matrix[7],
          node.Matrix[8], node.Matrix[9], node.Matrix[10], node.Matrix[11],
          node.Matrix[12], node.Matrix[13], node.Matrix[14], node.Matrix[15]
      );
      var mat = new Matrix4x4(
          node.Matrix[0], node.Matrix[4], node.Matrix[8], node.Matrix[12],
          node.Matrix[1], node.Matrix[5], node.Matrix[9], node.Matrix[13],
          node.Matrix[2], node.Matrix[6], node.Matrix[10], node.Matrix[14],
          node.Matrix[3], node.Matrix[7], node.Matrix[11], node.Matrix[15]
      );
      // Logger.Error("Returning mat");
      // Matrix4x4.Invert(mat1, out var result);
      return mat1;
    } else {
      Logger.Error("returning pos");
      // Otherwise, use TRS components and build the matrix
      var translation = node.Translation != null ? new Vector3(node.Translation[0], node.Translation[1], node.Translation[2]) : Vector3.Zero;
      var rotation = node.Rotation != null ? new Quaternion(node.Rotation[0], node.Rotation[1], node.Rotation[2], node.Rotation[3]) : Quaternion.Identity;
      var scale = node.Scale != null ? new Vector3(node.Scale[0], node.Scale[1], node.Scale[2]) : Vector3.One;
      var angleX = Converter.DegreesToRadians(node.Rotation[0]);
      var angleY = Converter.DegreesToRadians(node.Rotation[1]);
      var angleZ = Converter.DegreesToRadians(node.Rotation[2]);
      var rot = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);

      // Build the transformation matrix from TRS components
      var translationMatrix = Matrix4x4.CreateTranslation(translation);
      var rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
      var scaleMatrix = Matrix4x4.CreateScale(scale);

      // Combine the TRS matrices
      return scaleMatrix * rotationMatrix * translationMatrix;
    }
  }

  public static Matrix4x4[] GetInverseBindMatrices(Gltf gltf, byte[] globalBuffer, glTFLoader.Schema.Skin skin) {
    // Get the accessor index for the inverse bind matrices
    var accessorIndex = skin.InverseBindMatrices!.Value;

    // Get the accessor
    var accessor = gltf.Accessors[accessorIndex];

    // Get the buffer view index from the accessor
    var bufferViewIndex = accessor.BufferView!.Value;

    // Get the buffer view
    var bufferView = gltf.BufferViews[bufferViewIndex];

    // Get the buffer data
    var buffer = gltf.Buffers[bufferView.Buffer];

    // Extract the inverse bind matrices from the buffer
    return ExtractMatricesFromAccessor(globalBuffer, bufferView, accessor);
  }

  private static Matrix4x4[] ExtractMatricesFromAccessor(byte[] bufferData, BufferView bufferView, Accessor accessor) {
    // Calculate the starting position in the buffer
    int byteOffset = bufferView.ByteOffset + accessor.ByteOffset;

    // Calculate the number of matrices (each matrix is 64 bytes: 4x4 floats)
    int matrixCount = accessor.Count;
    Matrix4x4[] matrices = new Matrix4x4[matrixCount];

    // Read the matrices from the buffer data
    for (int i = 0; i < matrixCount; i++) {
      matrices[i] = new Matrix4x4(
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 0)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 4)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 8)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 12)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 16)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 20)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 24)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 28)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 32)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 36)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 40)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 44)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 48)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 52)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 56)),
          BitConverter.ToSingle(bufferData, byteOffset + (i * 64 + 60))
      );
    }

    return matrices;
  }

  private static uint[] GetIndexAccessor(Gltf gltf, byte[] globalBuffer, int accessorIdx) {
    var accessor = gltf.Accessors[accessorIdx];
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];
    var buffer = gltf.Buffers[bufferView.Buffer];

    uint[] indices;
    int byteOffset = bufferView.ByteOffset + accessor.ByteOffset;

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);
    indices = new uint[accessor.Count];
    for (int i = 0; i < accessor.Count; i++) {
      switch (accessor.ComponentType) {
        case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
          indices[i] = reader.ReadByte();
          break;
        case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
          indices[i] = reader.ReadUInt16();
          break;
        case Accessor.ComponentTypeEnum.UNSIGNED_INT:
          indices[i] = reader.ReadUInt32();
          break;
        default:
          throw new NotSupportedException("Unsupported index component type.");
      }
    }

    return indices;
  }

  public static float[] GetFloatAccessor(Gltf gltf, byte[] globalBuffer, Accessor accessor) {
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];

    var data = new float[accessor.Count];
    var byteOffset = bufferView.ByteOffset + accessor.ByteOffset;
    var stride = 4;

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);
    for (int i = 0; i < accessor.Count; i++) {
      data[i] = reader.ReadSingle();
      reader.BaseStream.Seek(stride - 4, SeekOrigin.Current);
    }

    return data;
  }
  public static void LoadAccessor<T>(Gltf gltf, byte[] globalBuffer, Accessor accessor, out T[][] data) {
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];

    data = new T[accessor.Count][];
    var byteOffset = bufferView.ByteOffset + accessor.ByteOffset;

    var typeResult = HandleType(accessor.Type, accessor.ComponentType);
    if (typeof(T) != typeResult.Item2)
      throw new ArgumentException($"{typeof(T)} does not match with {typeResult.Item2}");

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);

    for (int i = 0; i < accessor.Count; i++) {
      data[i] = new T[typeResult.Item1];
      for (int j = 0; j < typeResult.Item1; j++) {
        if (typeResult.Item2 == typeof(float)) {
          var value = reader.ReadSingle();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(short)) {
          var value = reader.ReadInt16();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(sbyte)) {
          var value = reader.ReadSByte();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(uint)) {
          var value = reader.ReadUInt32();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(byte)) {
          var value = reader.ReadByte();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(ushort)) {
          var value = reader.ReadUInt16();
          data[i][j] = (T)(object)value;
        } else {
          throw new InvalidCastException($"Given type {typeResult.Item2} cannot be parsed!");
        }
      }
    }
  }

  private static (int, Type) HandleType(Accessor.TypeEnum type, Accessor.ComponentTypeEnum componentType) {
    Type valueType;
    int elemPerVec;

    switch (componentType) {
      case Accessor.ComponentTypeEnum.BYTE:
        valueType = typeof(sbyte);
        break;
      case Accessor.ComponentTypeEnum.SHORT:
        valueType = typeof(short);
        break;
      case Accessor.ComponentTypeEnum.FLOAT:
        valueType = typeof(float);
        break;
      case Accessor.ComponentTypeEnum.UNSIGNED_INT:
        valueType = typeof(uint);
        break;
      case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
        valueType = typeof(byte);
        break;
      case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
        valueType = typeof(ushort);
        break;
      default:
        Logger.Error("Unknown Component Type!");
        throw new ArgumentException($"Unknown Component Type! {nameof(componentType)}");
    }

    switch (type) {
      case Accessor.TypeEnum.SCALAR:
        elemPerVec = 1;
        break;
      case Accessor.TypeEnum.VEC2:
        elemPerVec = 2;
        break;
      case Accessor.TypeEnum.VEC3:
        elemPerVec = 3;
        break;
      case Accessor.TypeEnum.VEC4:
        elemPerVec = 4;
        break;
      case Accessor.TypeEnum.MAT2:
        elemPerVec = 4;
        break;
      case Accessor.TypeEnum.MAT3:
        elemPerVec = 9;
        break;
      case Accessor.TypeEnum.MAT4:
        elemPerVec = 16;
        break;
      default:
        Logger.Error("Unknown Type!");
        throw new ArgumentException($"Unknown Type! {nameof(type)}");
    }

    return (elemPerVec, valueType);
  }

  public static Vector3 ToVector3(this float[] vec3) {
    return new Vector3(vec3[0], vec3[1], vec3[2]);
  }
  public static Vector3 ToVector3(this Vector4 vector4) {
    return new Vector3(vector4.X, vector4.Y, vector4.Z);
  }
  public static Vector3[] ToVector3Array(this float[][] vec3Array) {
    return vec3Array.Select(x => x.ToVector3()).ToArray();
  }

  public static Vector2 ToVector2(this float[] vec2) {
    return new Vector2(vec2[0], vec2[1]);
  }
  public static Vector2[] ToVector2Array(this float[][] vec2Array) {
    return vec2Array.Select(x => x.ToVector2()).ToArray();
  }

  public static Vector4 ToVector4(this float[] vec4) {
    if (vec4.Length < 4) {
      var returnVec = new Vector4();
      for (int i = 0; i < vec4.Length; i++) {
        returnVec[i] = vec4[i];
      }
      for (int i = vec4.Length; i < 4; i++) {
        returnVec[i] = 0;
      }
      return returnVec;
    } else {
      return new Vector4(vec4[0], vec4[1], vec4[2], vec4[3]);
    }
  }
  public static Vector4[] ToVector4Array(this float[][] vec4Array) {
    return vec4Array.Select(x => x.ToVector4()).ToArray();
  }

  public static float[] ToFloatArray(this float[][] floatArray) {
    return floatArray.SelectMany(x => x).ToArray();
  }

  public static Vector4I ToVec4I(this ushort[] batch) {
    return new Vector4I(batch[0], batch[1], batch[2], batch[3]);
  }
  public static Vector4I ToVec4I(this byte[] batch) {
    return new Vector4I(batch[0], batch[1], batch[2], batch[3]);
  }
  public static Vector4I[] ToVec4IArray(this ushort[][] ushorts) {
    return ushorts.Select(x => x.ToVec4I()).ToArray();
  }
  public static Vector4I[] ToVec4IArray(this byte[][] ushorts) {
    return ushorts.Select(x => x.ToVec4I()).ToArray();
  }

  public static Matrix4x4 ToMatrix4x4(this float[] floats) {
    var std = new Matrix4x4(
      floats[0], floats[1], floats[2], floats[3],
      floats[4], floats[5], floats[6], floats[7],
      floats[8], floats[9], floats[10], floats[11],
      floats[12], floats[13], floats[14], floats[15]
    );
    var alt = new Matrix4x4(
      floats[0], floats[4], floats[8], floats[12],
      floats[1], floats[5], floats[9], floats[13],
      floats[2], floats[6], floats[10], floats[14],
      floats[3], floats[7], floats[11], floats[15]
    );

    return std;
  }
  public static Matrix4x4[] ToMatrix4x4Array(this float[][] floats) {
    return floats.Select(x => x.ToMatrix4x4()).ToArray();
  }

  public static Quaternion ToQuat(this float[] floats) {
    return new Quaternion(floats[0], floats[1], floats[2], floats[3]);
  }
}