using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Loaders;
using Dwarf.Vulkan;
using glTFLoader.Schema;
using Vortice.Vulkan;

namespace Dwarf.Model.Animation;

public class Skin : IDisposable {
  public string Name { get; private set; } = default!;
  public DwarfBuffer Ssbo = null!;
  private VkDescriptorSet _descriptorSet = VkDescriptorSet.Null;

  public Node SkeletonRoot = null!;
  public List<Matrix4x4> InverseBindMatrices;
  public List<Node> Joints;

  public List<Matrix4x4> OutputNodeMatrices;
  public int JointsCount;

  public List<Animation> Animations;

  public Node MeshNode { get; private set; }

  public Skin() {

  }

  public void CreateBuffer() {
    Ssbo = new DwarfBuffer(
      Application.Instance.Device,
      (ulong)Unsafe.SizeOf<Matrix4x4>() * 24,
      BufferUsage.StorageBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );
    Ssbo.Map();
  }

  public unsafe void UpdateAnimation(int idx, float time) {
    if (Animations.Count < 1) {
      Logger.Error(".glTF does not contain animation.");
      return;
    }

    if (idx > Animations.Count - 1) {
      Logger.Error($"No animation with index {idx}");
      return;
    }

    bool updated = false;
    var animation = Animations[idx];
    foreach (var channel in animation.Channels) {
      var sampler = animation.Samplers[channel.SamplerIndex];
      if (sampler.Inputs.Count > sampler.OutputsVec4.Count) {
        continue;
      }
      for (int i = 0; i < sampler.Inputs.Count; i++) {
        if ((time >= sampler.Inputs[i]) && (time <= sampler.Inputs[i + 1])) {
          float u = MathF.Max(0.0f, time - sampler.Inputs[i]) / (sampler.Inputs[i + 1] - sampler.Inputs[i]);
          if (u <= 1.0f) {
            switch (channel.Path) {
              case AnimationChannel.PathType.Translation:
                sampler.Translate(idx, time, channel.Node);
                break;
              case AnimationChannel.PathType.Rotation:
                sampler.Rotate(idx, time, channel.Node);
                break;
              case AnimationChannel.PathType.Scale:
                sampler.Scale(idx, time, channel.Node);
                break;
            }
            updated = true;
          }
        }
      }
    }
    if (updated) {
      // foreach(var node in )
    }
  }

  public void LoadAnimations(Gltf gltf, byte[] globalBuffer) {
    foreach (var anim in gltf.Animations) {
      var animation = new Animation();
      animation.Name = anim.Name;
      if (anim.Name == string.Empty) {
        animation.Name = Animations.Count.ToString();
      }

      // Samplers
      foreach (var samp in anim.Samplers) {
        var sampler = new AnimationSampler();

        if (samp.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.LINEAR) {
          sampler.Interpolation = AnimationSampler.InterpolationType.Linear;
        } else if (samp.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.STEP) {
          sampler.Interpolation = AnimationSampler.InterpolationType.Step;
        } else if (samp.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.CUBICSPLINE) {
          sampler.Interpolation = AnimationSampler.InterpolationType.CubicSpline;
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

        }
      }
    }
  }

  /*
  public void Init(Gltf gltf, byte[] globalBuffer) {
    var gltfSkin = gltf.Skins[0];

    if (gltfSkin.InverseBindMatrices.HasValue) {
      var numofJoints = gltfSkin.Joints.Length;
      Skeleton.Joints = new Joint[numofJoints];
      Skeleton.FinalJointMatrices = new Matrix4x4[numofJoints];
      Skeleton.Name = gltfSkin.Name;

      var inverseBindMatrices = GLTFLoaderKHR.GetInverseBindMatrices(gltf, globalBuffer, gltfSkin);

      var accessorIdx = gltfSkin.InverseBindMatrices.Value;

      // TODO check if both matrices are equal

      GLTFLoaderKHR.LoadAccessor<float>(gltf, globalBuffer, gltf.Accessors[accessorIdx], out var matFloats);
      var matArr = matFloats.ToMatrix4x4Array();

      if (!Matrix4x4.Equals(inverseBindMatrices, matArr)) {
        Logger.Error("Matrices are not the same!!!!");
      }

      for (int jointIdx = 0; jointIdx < numofJoints; jointIdx++) {
        // inverseBindMatrices[jointIdx].M42 = -inverseBindMatrices[jointIdx].M42;
        int globalIdx = gltfSkin.Joints[jointIdx];
        Skeleton.Joints[jointIdx] = new Joint {
          InverseBindMatrix = matArr[jointIdx],
          Name = gltf.Nodes[globalIdx].Name,
          Node = gltf.Nodes[globalIdx],
        };
        // Skeleton.Joints[jointIdx].Node.Translation[1] *= -1;

        Skeleton.GlobalNodeToJointIdx[globalIdx] = jointIdx;
      }

      int rootJoint = gltfSkin.Joints[0];
      LoadJoint(gltf, rootJoint, Skeleton.NO_PARENT);
    }

    Ssbo = new DwarfBuffer(
      Application.Instance.Device,
      (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)Skeleton.Joints.Length,
      BufferUsage.StorageBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );
    Ssbo.Map();

    // Load Animations

    if (gltf.Animations == null) {
      Skeleton.IsAnimated = false;
      return;
    }

    var numberOfAnimations = gltf.Animations.Length;
    for (int animationIndex = 0; animationIndex < numberOfAnimations; animationIndex++) {
      var gltfAnimation = gltf.Animations[animationIndex];
      Logger.Info($"Animation: {gltfAnimation.Name}");
      var animation = new SkeletalAnimation(gltfAnimation.Name);

      // samplers
      int samplersCount = gltfAnimation.Samplers.Length;
      animation.Samplers = new SkeletalAnimation.Sampler[samplersCount];
      for (int samplerIndex = 0; samplerIndex < samplersCount; samplerIndex++) {
        // var gltfSampler = gltf.Animations[].Samplers[samplerIndex];
        var gltfSampler = gltfAnimation.Samplers[samplerIndex];

        animation.Samplers[samplerIndex] = new SkeletalAnimation.Sampler {
          Interpolation = SkeletalAnimation.InterpolationMethod.Linear
        };
        if (gltfSampler.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.STEP) {
          animation.Samplers[samplerIndex].Interpolation = SkeletalAnimation.InterpolationMethod.Step;
        } else if (gltfSampler.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.CUBICSPLINE) {
          animation.Samplers[samplerIndex].Interpolation = SkeletalAnimation.InterpolationMethod.CubicSpline;
        }

        // get timestamp
        {
          int count = 0;
          // var acc = model.LogicalAccessors[gltfSampler.Input].AsScalarArray();
          var acc = gltf.Accessors[gltfSampler.Input];
          // GLTFLoaderKHR.LoadAccessor<float>(gltf, globalBuffer, acc, out var floatArray);
          var flat = GLTFLoaderKHR.GetFloatAccessor(gltf, globalBuffer, acc);
          count = acc.Count;
          animation.Samplers[samplerIndex].Timestamps = new float[count];
          for (int index = 0; index < count; index++) {
            animation.Samplers[samplerIndex].Timestamps[index] = flat[index];
          }
        }

        // read sampler keyframes
        // POSSIBLE BUG HERE
        {
          var acc = gltf.Accessors[gltfSampler.Output];
          GLTFLoaderKHR.LoadAccessor<float>(gltf, globalBuffer, acc, out var outArray);
          var vec4Array = outArray.ToVector4Array();
          int count = acc.Count;
          animation.Samplers[samplerIndex].TRSOutputValuesToBeInterpolated = new Vector4[count];
          for (int index = 0; index < count; index++) {
            vec4Array[index].Y *= -1;
            animation.Samplers[samplerIndex].TRSOutputValuesToBeInterpolated[index] = vec4Array[index];
          }
        }

        if (animation.Samplers.Length > 0) {
          var sampler = animation.Samplers[samplerIndex];
          if (sampler.Timestamps.Length >= 2) {
            animation.SetFirstKeyFrameTime(sampler.Timestamps[0]);
            animation.SetLastKeyFrameTime(sampler.Timestamps.Last());
          }

          var channelsCount = gltfAnimation.Channels.Length;
          animation.Channels = new SkeletalAnimation.Channel[channelsCount];
          for (int channelIndex = 0; channelIndex < channelsCount; channelIndex++) {
            var gltfChannel = gltfAnimation.Channels[channelIndex];

            animation.Channels[channelIndex].SamplerIndex = gltfChannel.Sampler;
            animation.Channels[channelIndex].Node = (int)gltfChannel.Target.Node!;

            if (gltfChannel.Target.Path == AnimationChannelTarget.PathEnum.translation) {
              animation.Channels[channelIndex].Path = SkeletalAnimation.Path.Translation;
            } else if (gltfChannel.Target.Path == AnimationChannelTarget.PathEnum.rotation) {
              animation.Channels[channelIndex].Path = SkeletalAnimation.Path.Rotation;
            } else if (gltfChannel.Target.Path == AnimationChannelTarget.PathEnum.scale) {
              animation.Channels[channelIndex].Path = SkeletalAnimation.Path.Scale;
            } else {
              Logger.Error($"[Skin::Init] Path not supported");
            }
          }

          Animations.Add(animation);
          SkeletonAnimations.AddAnimation(animation);
        }
      }
    }
  }

  public void LoadJoint(Gltf gltf, int globalJointIdx, int parent) {
    var current = Skeleton.GlobalNodeToJointIdx[globalJointIdx];
    Skeleton.Joints[current].ParentJoint = parent;

    if (gltf.Nodes[globalJointIdx].Children == null) {
      Skeleton.Joints[current].Children = [];
    } else {
      var childCount = gltf.Nodes[globalJointIdx].Children.Length;
      if (childCount > 0) {
        Skeleton.Joints[current].Children = new int[childCount];
        var children = gltf.Nodes[globalJointIdx].Children;
        for (int i = 0; i < childCount; i++) {
          var globalGltfNodeIdxForChild = children[i];
          Skeleton.Joints[current].Children[i] = Skeleton.GlobalNodeToJointIdx[globalGltfNodeIdxForChild];
          LoadJoint(gltf, globalGltfNodeIdxForChild, current);
        }
      }
    }
  }
  */

  public void BuildDescriptor(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    unsafe {
      var range = Ssbo.GetDescriptorBufferInfo(Ssbo.GetAlignmentSize());
      range.range = Ssbo.GetAlignmentSize();

      _ = new VulkanDescriptorWriter(descriptorSetLayout, descriptorPool)
      .WriteBuffer(0, &range)
      .Build(out _descriptorSet);
    }
  }

  public unsafe void WriteSkeleton() {
    fixed (Matrix4x4* ibmPtr = OutputNodeMatrices.ToArray()) {
      Ssbo.WriteToBuffer((nint)ibmPtr, Ssbo.GetAlignmentSize());
    }
    Ssbo.Flush();
  }

  public unsafe void WriteSkeletonIdentity() {
    if (InverseBindMatrices.Count < 1) {
      WriteIdentity();
      return;
    }
    var mats = new Matrix4x4[InverseBindMatrices.Count];
    for (int i = 0; i < mats.Length; i++) {
      mats[i] = Matrix4x4.Identity;
    }
    fixed (Matrix4x4* matsPtr = mats) {
      Ssbo.WriteToBuffer((nint)matsPtr, Ssbo.GetAlignmentSize());
    }
  }

  public unsafe void WriteIdentity() {
    var mats = new Matrix4x4[24];
    for (int i = 0; i < mats.Length; i++) {
      mats[i] = Matrix4x4.Identity;
    }
    fixed (Matrix4x4* matsPtr = mats) {
      Ssbo.WriteToBuffer((nint)matsPtr, Ssbo.GetAlignmentSize());
    }
  }

  public unsafe void Write(nint data, ulong size, ulong offset) {
    Ssbo.WriteToBuffer(data, size, offset);
  }

  public unsafe void Write(Matrix4x4 data, ulong size, ulong offset) {
    Ssbo.WriteToBuffer((nint)(&data), size, offset);
  }

  public void Dispose() {
    Ssbo?.Dispose();
  }

  public VkDescriptorSet DescriptorSet => _descriptorSet;
}
