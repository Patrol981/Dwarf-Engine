using System.Numerics;
using System.Runtime.CompilerServices;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Model.Animation;
using Dwarf.Utils;
using glTFLoader;
using glTFLoader.Schema;

namespace Dwarf.Loaders;

public static class GLTFLoaderKHR {
  public static async Task<MeshRenderer> LoadGLTF(Application app, string path, bool preload = false, int flip = 1) {
    var gltf = Interface.LoadModel(path);
    var glb = Interface.LoadBinaryBuffer(path);

    string[] paths;

    var meshMaterialPair = new Dictionary<Mesh, int>();

    foreach (var node in gltf.Nodes) {
      if (node.Mesh.HasValue) {
        ProcessMesh(gltf, glb, node, ref meshMaterialPair);
      }
    }
    ProcessMaterial(gltf, glb, meshMaterialPair, path, out paths);
    ProcessSkeleton(gltf, glb, ref meshMaterialPair);

    var textures = await TextureManager.AddTextures(app.Device, paths, flip);
    app.TextureManager.AddRange([.. textures]);

    var meshRenderer = new MeshRenderer(app.Device, app.Renderer, [.. meshMaterialPair.Keys]);

    if (meshRenderer.MeshsesCount == textures.Length || textures.Length > meshRenderer.MeshsesCount) {
      meshRenderer.BindMultipleModelPartsToTextures(app.TextureManager, paths);
    } else if (paths.Length > 0) {
      meshRenderer.BindMultipleModelPartsToTexture(app.TextureManager, paths[0]);
    } else {
      meshRenderer.BindMultipleModelPartsToTexture(app.TextureManager, "./Resources/Textures/base/no_texture.png");
    }

    meshRenderer.Meshes[0].Skin.SkeletonAnimations.Start("Walk");
    meshRenderer.Meshes[0].Skin.SkeletonAnimations.SetRepeat(true);

    // meshRenderer.Meshes[0].Skin.Skeleton.Traverse();

    return meshRenderer;
  }

  private static void ProcessSkeleton(
    Gltf gltf,
    byte[] globalBuffer,
    ref Dictionary<Mesh, int> meshMaterialPair
  ) {
    foreach (var mesh in meshMaterialPair.Keys) {
      List<Matrix4x4> matrices = [];
      for (int i = 0; i < 25; i++) {
        matrices.Add(Matrix4x4.Identity);
      }
      Skeleton skeleton = new();
      mesh.Skin = new(skeleton);
      mesh.Skin.Init(gltf, globalBuffer);
    }
  }

  private static void ProcessMaterial(
    Gltf gltf,
    byte[] globalBuffer,
    Dictionary<Mesh, int> meshMaterialPair,
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
    Gltf gltf,
    byte[] globalBuffer,
    glTFLoader.Schema.Node mesh,
    ref Dictionary<Mesh, int> meshMaterialPair
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
        LoadAccessor<Vector2>(gltf, globalBuffer, gltf.Accessors[coordIdx], out var texFloats);
        textureCoords = texFloats.ToVector2Array();
      }
      if (primitive.Attributes.TryGetValue("POSITION", out int positionIdx)) {
        LoadAccessor<Vector3>(gltf, globalBuffer, gltf.Accessors[positionIdx], out var posFloats);
        positions = posFloats.ToVector3Array();
      }
      if (primitive.Attributes.TryGetValue("NORMAL", out int normalIdx)) {
        LoadAccessor<Vector3>(gltf, globalBuffer, gltf.Accessors[normalIdx], out var normFloats);
        normals = normFloats.ToVector3Array();
      }
      if (primitive.Attributes.TryGetValue("WEIGHTS_0", out int weightsIdx)) {
        LoadAccessor<Vector4>(gltf, globalBuffer, gltf.Accessors[weightsIdx], out var weightFLoats);
        weights = weightFLoats.ToVector4Array();
      }
      if (primitive.Attributes.TryGetValue("JOINTS_0", out int jointsIdx)) {
        LoadAccessorUint(gltf, globalBuffer, gltf.Accessors[jointsIdx], out var jointIndices);
        joints = jointIndices.ToVec4IArray();
      }
      if (primitive.Indices.HasValue) {
        var idx = primitive.Indices.Value;
        var idc = GetIndexAccessor(gltf, globalBuffer, idx);
        indices.AddRange(idc);
      }

      var vertex = new Vertex();
      var nodeMat = GetNodeMatrix(mesh);
      nodeMatrices.Add(nodeMat);
      for (int i = 0; i < positions.Length; i++) {
        vertex.Position = Vector3.Transform(positions[i], nodeMat);
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

    var meshData = new Mesh {
      Vertices = [.. vertices],
      Indices = [.. indices],
      Skin = null,
      NodeMatrices = [.. nodeMatrices]
    };

    meshMaterialPair.Add(meshData, materialId);
  }

  public static Matrix4x4 GetNodeMatrix(Node node) {
    if (node.Matrix != null && node.Matrix.Length == 16) {
      return new Matrix4x4(
          node.Matrix[0], node.Matrix[1], node.Matrix[2], node.Matrix[3],
          node.Matrix[4], node.Matrix[5], node.Matrix[6], node.Matrix[7],
          node.Matrix[8], node.Matrix[9], node.Matrix[10], node.Matrix[11],
          node.Matrix[12], node.Matrix[13], node.Matrix[14], node.Matrix[15]
      );
    } else {
      // Otherwise, use TRS components and build the matrix
      var translation = node.Translation != null ? new Vector3(node.Translation[0], node.Translation[1], node.Translation[2]) : Vector3.Zero;
      var rotation = node.Rotation != null ? new Quaternion(node.Rotation[0], node.Rotation[1], node.Rotation[2], node.Rotation[3]) : Quaternion.Identity;
      var scale = node.Scale != null ? new Vector3(node.Scale[0], node.Scale[1], node.Scale[2]) : Vector3.One;

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

  public static void LoadAccessorUint(Gltf gltf, byte[] globalBuffer, Accessor accessor, out ushort[][] data) {
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];

    data = new ushort[accessor.Count][];
    var byteOffset = bufferView.ByteOffset + accessor.ByteOffset;
    int stride;
    if (bufferView.ByteStride.HasValue) {
      stride = bufferView.ByteStride.Value;
    } else {
      stride = sizeof(uint);
    }

    var elemPerVec = 4;
    var strideMinus = 4;

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);
    for (int i = 0; i < accessor.Count; i++) {
      data[i] = new ushort[elemPerVec];
      for (int j = 0; j < elemPerVec; j++) {
        data[i][j] = reader.ReadUInt16();
      }
      reader.BaseStream.Seek(stride - stride, SeekOrigin.Current);
    }
  }

  public static void LoadAccessor<T>(Gltf gltf, byte[] globalBuffer, Accessor accessor, out float[][] data) {
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];

    data = new float[accessor.Count][];
    var byteOffset = bufferView.ByteOffset + accessor.ByteOffset;
    int stride;
    if (bufferView.ByteStride.HasValue) {
      stride = bufferView.ByteStride.Value;
    } else {
      stride = Unsafe.SizeOf<T>();
    }

    var elemPerVec = 0;
    var strideMinus = 0;
    if (typeof(T) == typeof(Vector3)) {
      elemPerVec = 3;
      strideMinus = 12;
    }
    if (typeof(T) == typeof(Vector4)) {
      elemPerVec = 4;
      strideMinus = 16;
    }
    if (typeof(T) == typeof(Vector2)) {
      elemPerVec = 2;
      strideMinus = 8;
    }
    if (typeof(T) == typeof(Matrix4x4)) {
      elemPerVec = 16;
      strideMinus = 64;
    }
    if (typeof(T) == typeof(float)) {
      elemPerVec = 1;
      strideMinus = 4;
    }
    if (typeof(T) == typeof(uint)) {
      elemPerVec = 1;
      strideMinus = 4;
    }

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);
    for (int i = 0; i < accessor.Count; i++) {
      data[i] = new float[elemPerVec];
      for (int j = 0; j < elemPerVec; j++) {
        data[i][j] = reader.ReadSingle();
      }
      reader.BaseStream.Seek(stride - strideMinus, SeekOrigin.Current);
    }
  }

  public static Vector3 ToVector3(this float[] vec3) {
    return new Vector3(vec3[0], vec3[1], vec3[2]);
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
    return new Vector4(vec4[0], vec4[1], vec4[2], vec4[3]);
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
  public static Vector4I[] ToVec4IArray(this ushort[][] ushorts) {
    return ushorts.Select(x => x.ToVec4I()).ToArray();
  }
}