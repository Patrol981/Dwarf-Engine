using System.Numerics;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Utils;
using Dwarf.Vulkan;
using glTFLoader;
using glTFLoader.Schema;
using Vortice.Vulkan;
using static Dwarf.VulkanTexture;

namespace Dwarf.Loaders;

public static partial class GLTFLoaderKHR {
  public unsafe static Task<MeshRenderer> LoadGLTF(Application app, string path, int flip = 1) {
    var gltf = Interface.LoadModel(path);
    var glb = Interface.LoadBinaryBuffer(path);

    LoadTextureSamplers(gltf, out var textureSamplers);
    LoadTextures(app, path, gltf, glb, textureSamplers, flip, out var textureIds);
    LoadMaterials(gltf, out var materials);

    var meshRenderer = new MeshRenderer(app.Device, app.Renderer);
    var scene = gltf.Scenes[gltf.Scene.HasValue ? gltf.Scene.Value : 0];
    for (int i = 0; i < scene.Nodes.Length; i++) {
      var node = gltf.Nodes[scene.Nodes[i]];
      LoadNode(
        app.VmaAllocator,
        app.Device,
        null!,
        node,
        scene.Nodes[i],
        gltf,
        glb,
        1.0f,
        ref meshRenderer,
        ref materials
      );
    }

    if (gltf.Animations != null && gltf.Animations.Length > 0) {
      LoadAnimations(gltf, glb, meshRenderer);
    }

    LoadSkins(gltf, glb, meshRenderer);

    foreach (var node in meshRenderer.LinearNodes) {
      if (node.SkinIndex > -1) {
        node.Skin = meshRenderer.Skins[node.SkinIndex];
        // node.CreateBuffer();
      }

      if (node.Mesh != null) {
        var material = node.Mesh.Material;

        node.Update();
      }
    }

    meshRenderer.Init();

    // meshRenderer.BindToTexture(app.TextureManager, textureIds[0], 0);
    // meshRenderer.BindToTexture(app.TextureManager, textureIds[5], 1);
    // meshRenderer.BindToTexture(app.TextureManager, textureIds[1], 2);

    for (int i = 0; i < meshRenderer.MeshedNodesCount; i++) {
      meshRenderer.BindToTextureMaterial(app.TextureManager, textureIds, i);
    }

    // if (meshRenderer.MeshedNodes.Length == textureIds.Count) {
    //   for (int i = 0; i < meshRenderer.MeshedNodes.Length; i++) {
    //     meshRenderer.BindToTexture(app.TextureManager, textureIds[i], i);
    //   }
    // } else {
    //   for (int i = 0; i < meshRenderer.MeshedNodes.Length; i++) {
    //     meshRenderer.BindToTexture(app.TextureManager, textureIds[0], i);
    //   }
    // }

    return Task.FromResult(meshRenderer);
  }

  private static void LoadTextureSamplers(Gltf gltf, out List<TextureSampler> textureSamplers) {
    textureSamplers = [];
    foreach (var sampler in gltf.Samplers) {
      var textureSampler = new TextureSampler {
        MinFilter = GetFilterMode((int)sampler.MinFilter!),
        MagFilter = GetFilterMode((int)sampler.MagFilter!),
        AddressModeU = GetWrapMode((int)sampler.WrapS),
        AddressModeV = GetWrapMode((int)sampler.WrapT)
      };
      textureSampler.AddressModeW = textureSampler.AddressModeV;
      textureSamplers.Add(textureSampler);
    }
  }

  private static void LoadTextures(
    Application app,
    in string textureName,
    in Gltf gltf,
    in byte[] globalBuffer,
    in List<TextureSampler> textureSamplers,
    int flip,
    out List<(Guid id, ITexture texture)> textureIds
  ) {
    textureIds = [];
    int i = 0;
    foreach (var gltfTexture in gltf.Textures) {
      if (!gltfTexture.Source.HasValue) continue;
      int src = gltfTexture.Source.Value;

      var gltfImage = gltf.Images[src];
      var textureSampler = new TextureSampler();
      if (!gltfTexture.Sampler.HasValue) {
        textureSampler.MagFilter = VkFilter.Linear;
        textureSampler.MinFilter = VkFilter.Linear;
        textureSampler.AddressModeU = VkSamplerAddressMode.Repeat;
        textureSampler.AddressModeV = VkSamplerAddressMode.Repeat;
        textureSampler.AddressModeW = VkSamplerAddressMode.Repeat;
      } else {
        textureSampler = textureSamplers[gltfTexture.Sampler.Value];
      }

      var id = app.TextureManager.GetTextureIdLocal($"{textureName}_{textureIds.Count}");
      ITexture texture = null!;
      if (id == Guid.Empty) {
        texture = VulkanTexture.LoadFromGLTF(
          app.VmaAllocator,
          app.Device,
          gltf,
          globalBuffer,
          gltfImage,
          $"{textureName}_{textureIds.Count}",
          textureSampler,
          flip
        );
        id = app.TextureManager.AddTextureLocal(texture);
        texture.TextureIndex = i;

        // var path = Path.Combine(DwarfPath.AssemblyDirectory, $"{textureName}_{textureIds.Count}.png");
        //File.WriteAllBytes(path, texture.TextureData);
      }
      i++;
      textureIds.Add((id, texture));
    }
  }

  private static void LoadMaterials(Gltf gltf, out List<Material> materials) {
    materials = [];

    foreach (var mat in gltf.Materials) {
      var material = new Material(mat.Name);
      material.DoubleSided = material.DoubleSided;

      if (mat.PbrMetallicRoughness != null) {
        if (mat.PbrMetallicRoughness.BaseColorTexture != null) {
          material.BaseColorTextureIndex = mat.PbrMetallicRoughness.BaseColorTexture.Index;
        }
        if (mat.PbrMetallicRoughness.MetallicRoughnessTexture != null) {
          material.MetallicRoughnessTextureIndex = mat.PbrMetallicRoughness.MetallicRoughnessTexture.Index;
        }
      }

      if (mat.NormalTexture != null) {
        material.NormalTextureIndex = mat.NormalTexture.Index;
      }
      if (mat.OcclusionTexture != null) {
        material.OcclusionTextureIndex = mat.OcclusionTexture.Index;
      }
      if (mat.EmissiveTexture != null) {
        material.EmissiveTextureIndex = mat.EmissiveTexture.Index;
      }

      if (mat.ShouldSerializeAlphaMode()) {
        switch (mat.AlphaMode) {
          case glTFLoader.Schema.Material.AlphaModeEnum.OPAQUE:
            material.AlphaMode = AlphaMode.Opaque;
            break;
          case glTFLoader.Schema.Material.AlphaModeEnum.MASK:
            material.AlphaMode = AlphaMode.Mask;
            break;
          case glTFLoader.Schema.Material.AlphaModeEnum.BLEND:
            material.AlphaMode = AlphaMode.Blend;
            break;
        }
      }

      materials.Add(material);
    }

    materials.Add(new());
  }

  private static void LoadAnimations(Gltf gltf, byte[] globalBuffer, MeshRenderer meshRenderer) {
    foreach (var anim in gltf.Animations) {
      var animation = new Dwarf.Model.Animation.Animation();
      animation.Name = anim.Name;
      if (anim.Name == string.Empty) {
        animation.Name = meshRenderer.Animations.Count.ToString();
      }

      // Samplers
      foreach (var samp in anim.Samplers) {
        var sampler = new Dwarf.Model.Animation.AnimationSampler();

        if (samp.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.LINEAR) {
          sampler.Interpolation = Dwarf.Model.Animation.AnimationSampler.InterpolationType.Linear;
        } else if (samp.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.STEP) {
          sampler.Interpolation = Dwarf.Model.Animation.AnimationSampler.InterpolationType.Step;
        } else if (samp.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.CUBICSPLINE) {
          sampler.Interpolation = Dwarf.Model.Animation.AnimationSampler.InterpolationType.CubicSpline;
        }

        // Read sampler input time values
        {
          var acc = gltf.Accessors[samp.Input];
          var flat = GLTFLoaderKHR.GetFloatAccessor(gltf, globalBuffer, acc);
          int count = acc.Count;
          sampler.Inputs = [];
          for (int index = 0; index < count; index++) {
            sampler.Inputs.Add(flat[index]);
          }

          foreach (var input in sampler.Inputs) {
            if (input < animation.Start) {
              animation.Start = input;
            }
            if (input > animation.End) {
              animation.End = input;
            }
          }
        }

        // Read sampler T/R/S values
        {
          var acc = gltf.Accessors[samp.Output];
          GLTFLoaderKHR.LoadAccessor<float>(gltf, globalBuffer, acc, out var floatArray);
          sampler.OutputsVec4 = [.. floatArray.ToVector4Array()];
        }

        animation.Samplers.Add(sampler);
      }

      // channels
      foreach (var source in anim.Channels) {
        var channel = new Dwarf.Model.Animation.AnimationChannel();

        if (source.Target.Path == AnimationChannelTarget.PathEnum.rotation) {
          channel.Path = Dwarf.Model.Animation.AnimationChannel.PathType.Rotation;
        }
        if (source.Target.Path == AnimationChannelTarget.PathEnum.translation) {
          channel.Path = Dwarf.Model.Animation.AnimationChannel.PathType.Translation;
        }
        if (source.Target.Path == AnimationChannelTarget.PathEnum.scale) {
          channel.Path = Dwarf.Model.Animation.AnimationChannel.PathType.Scale;
        }
        if (source.Target.Path == AnimationChannelTarget.PathEnum.weights) {
          Logger.Warn("Weights not supported, skipping channel");
          continue;
        }

        channel.SamplerIndex = source.Sampler;
        var foundNode = meshRenderer.NodeFromIndex(source.Target.Node!.Value!);
        if (foundNode != null) {
          channel.Node = foundNode;
        }
        if (channel.Node == null) {
          continue;
        }

        animation.Channels.Add(channel);
      }

      meshRenderer.Animations.Add(animation);
    }
  }

  private static void LoadSkins(Gltf gltf, byte[] globalBuffer, MeshRenderer meshRenderer) {
    if (gltf.Skins == null) return;

    foreach (var source in gltf.Skins) {
      var newSkin = new Dwarf.Model.Animation.Skin();
      newSkin.Name = source.Name;

      // find skeleton root node
      if (source.Skeleton.HasValue) {
        var rootResult = meshRenderer.NodeFromIndex(source.Skeleton!.Value!);
        if (rootResult != null) {
          newSkin.SkeletonRoot = rootResult;
        }
      }

      // find joint nodes
      foreach (var jointIdx in source.Joints) {
        var node = meshRenderer.NodeFromIndex(jointIdx);
        if (node != null) {
          newSkin.Joints.Add(node);
        }
      }
      if (newSkin.Joints != null) {
        // newSkin.OutputNodeMatrices = new Matrix4x4[newSkin.Joints.Count];
        newSkin.Init();
      }

      // get inverse bind matrices
      if (source.InverseBindMatrices.HasValue) {
        var acc = gltf.Accessors[source.InverseBindMatrices.Value!];
        GLTFLoaderKHR.LoadAccessor<float>(gltf, globalBuffer, acc, out var floats);
        newSkin.InverseBindMatrices = [.. floats.ToMatrix4x4Array()];
      }

      meshRenderer.Skins.Add(newSkin);
    }
  }

  private static VkFilter GetFilterMode(int filterMode) {
    switch (filterMode) {
      case -1:
      case 9728:
        return VkFilter.Nearest;
      case 9729:
        return VkFilter.Linear;
      case 9984:
        return VkFilter.Nearest;
      case 9985:
        return VkFilter.Nearest;
      case 9986:
        return VkFilter.Linear;
      case 9987:
        return VkFilter.Linear;
    }

    throw new ArgumentException($"Unknkown filter {filterMode}");
  }

  private static VkSamplerAddressMode GetWrapMode(int wrapMode) {
    switch (wrapMode) {
      case -1:
      case 10497:
        return VkSamplerAddressMode.Repeat;
      case 33071:
        return VkSamplerAddressMode.ClampToEdge;
      case 33648:
        return VkSamplerAddressMode.MirroredRepeat;
    }

    throw new ArgumentException($"Unknown wrap mode {wrapMode}");
  }

  private static void LoadNode(
    VmaAllocator vmaAllocator,
    IDevice device,
    Dwarf.Model.Node parent,
    Node node,
    int nodeIdx,
    Gltf gltf,
    byte[] globalBuffer,
    float globalScale,
    ref MeshRenderer meshRenderer,
    ref List<Material> materials
  ) {
    Dwarf.Model.Node newNode = new() {
      Index = nodeIdx,
      Name = node.Name,
      SkinIndex = node.Skin.HasValue ? node.Skin.Value : -1,
      NodeMatrix = Matrix4x4.Identity
    };
    if (parent != null) {
      newNode.Parent = parent;
    }

    // Generate local node matrix
    var translation = Vector3.Zero;
    if (node.Translation.Length == 3) {
      translation = node.Translation.ToVector3();
      newNode.Translation = translation;
    }
    var rotation = Matrix4x4.Identity;
    if (node.Rotation.Length == 4) {
      var q = node.Rotation.ToQuat();
      newNode.Rotation = q;
    }
    var scale = Vector3.One;
    if (node.Scale.Length == 3) {
      scale = node.Scale.ToVector3();
      newNode.Scale = scale;
    }
    if (node.Matrix.Length == 16) {
      newNode.NodeMatrix = node.Matrix.ToMatrix4x4();
    }

    // Node with children
    if (node.Children?.Length > 0) {
      for (int i = 0; i < node.Children.Length; i++) {
        LoadNode(
          vmaAllocator,
          device,
          newNode,
          gltf.Nodes[node.Children[i]],
          node.Children[i],
          gltf,
          globalBuffer,
          globalScale,
          ref meshRenderer,
          ref materials
        );
      }
    }

    // Node contains mesh data
    if (node.Mesh.HasValue) {
      var gltfMesh = gltf.Meshes[node.Mesh.Value];
      var newMesh = new Mesh(vmaAllocator, device, newNode.NodeMatrix);

      var indices = new List<uint>();
      var vertices = new List<Vertex>();
      Material material = null!;

      for (int j = 0; j < gltfMesh.Primitives.Length; j++) {
        var primitive = gltfMesh.Primitives[j];

        int materialIdx = primitive.Material.HasValue ? primitive.Material.Value : -1;
        material = materialIdx >= 0 && materialIdx < materials.Count ? materials[materialIdx] : new Material();

        Vector2[] textureCoords = [];
        Vector3[] normals = [];
        Vector4[] weights = [];
        Vector4I[] joints = [];
        Vector3[] positions = [];

        // Vertices
        {
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

          var vertex = new Vertex();
          for (int v = 0; v < positions.Length; v++) {
            vertex.Position = positions[v];
            // vertex.Position = Vector3.Transform(positions[v], newNode.GetLocalMatrix());
            vertex.Color = Vector3.One;
            vertex.Normal = normals.Length > 0 ? normals[v] : new Vector3(1, 1, 1);
            vertex.Uv = textureCoords.Length > 0 ? textureCoords[v] : new Vector2(0, 0);

            vertex.JointWeights = weights.Length > 0 ? weights[v] : new Vector4(0, 0, 0, 0);
            vertex.JointIndices = joints.Length > 0 ? joints[v] : new Vector4I(0, 0, 0, 0);

            vertices.Add(vertex);
          }
        }

        if (primitive.Indices.HasValue) {
          var idx = primitive.Indices.Value;
          var idc = GetIndexAccessor(gltf, globalBuffer, idx);
          indices.AddRange(idc);
        }
      }

      newMesh.Vertices = [.. vertices];
      newMesh.Indices = [.. indices];
      material ??= new Material();
      newMesh.Material = material;
      newNode.Mesh = newMesh;
    }

    if (parent != null) {
      parent.Children.Add(newNode);
    } else {
      meshRenderer.AddNode(newNode);
    }
    meshRenderer.AddLinearNode(newNode);
  }
}