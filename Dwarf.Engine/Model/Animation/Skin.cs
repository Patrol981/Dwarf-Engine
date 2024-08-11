using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Loaders;
using Dwarf.Vulkan;
using glTFLoader.Schema;
using SharpGLTF.Collections;
using SharpGLTF.Schema2;
using Vortice.Vulkan;

namespace Dwarf.Model.Animation;

public class Skin : IDisposable {
  public string Name { get; private set; } = default!;
  public SharpGLTF.Schema2.Node SkeletonRoot { get; private set; } = null!;
  public IList<SharpGLTF.Schema2.Node> Joints { get; private set; } = [];
  public DwarfBuffer Ssbo = null!;
  private VkDescriptorSet _descriptorSet = VkDescriptorSet.Null;
  public ModelRoot GltfModel;

  public Skeleton Skeleton = null!;
  public List<SkeletalAnimation> Animations = [];
  public skeletalAnimations SkeletonAnimations = new();

  private Skin() {

  }

  public Skin(
    string name,
    SharpGLTF.Schema2.Node skeletonRoot,
    Matrix4x4[] inverseBindMatrices,
    IList<SharpGLTF.Schema2.Node> joints,
    DwarfBuffer ssbo,
    VkDescriptorSet descriptorSet
  ) {
    Name = name;
    SkeletonRoot = skeletonRoot;
    InverseBindMatrices = inverseBindMatrices;
    Joints = joints;
    Ssbo = ssbo;
    _descriptorSet = descriptorSet;
  }

  public Skin(Skeleton skeleton) {
    Skeleton = skeleton;
  }

  public void Setup(IDevice device) {
    if (Skeleton.FinalJointMatrices.Length > 0) {
      Ssbo = new DwarfBuffer(
        device,
        (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)Skeleton.FinalJointMatrices.Length,
        BufferUsage.StorageBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
    } else {
      Ssbo = new DwarfBuffer(
        device,
        (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)InverseBindMatrices.Length,
        BufferUsage.StorageBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
    }

  }
  public void Init(ModelRoot model, Gltf gltf) {
    GltfModel = model;
    var skin = model.LogicalSkins[0];

    if (skin.GetInverseBindMatricesAccessor() != null) {
      Skeleton.JointsData = new JointData[skin.JointsCount];
      Skeleton.FinalJointMatrices = new Matrix4x4[skin.JointsCount];

      Skeleton.Name = skin.Name;

      var inverseBindMatrices = skin.GetInverseBindMatricesAccessor().AsMatrix4x4Array();
      var targetClass = typeof(SharpGLTF.Schema2.Skin);
      var fieldInfo = targetClass.GetField("_joints", BindingFlags.NonPublic | BindingFlags.Instance);
      List<int> _joints = (List<int>)fieldInfo!.GetValue(skin)!;
      for (int i = 0; i < skin.JointsCount; ++i) {
        int globalIdx = _joints[i];
        Skeleton.JointsData[i] = new JointData {
          InverseBindMatrix = inverseBindMatrices[i],
          Name = model.LogicalNodes[globalIdx].Name,
          // Node = model.LogicalNodes[globalIdx]
        };

        Skeleton.GlobalNodeToJointIdx[globalIdx] = i;
      }

      var rootJoint = _joints[0];

      LoadJoint_Old(rootJoint, Skeleton.NO_PARENT);
    }

    Ssbo = new DwarfBuffer(
      Application.Instance.Device,
      (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)Skeleton.JointsData.Length,
      BufferUsage.StorageBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );
    Ssbo.Map();

    // Load animations

    var numberOfAnimations = gltf.Animations.Length;
    for (int i = 0; i < numberOfAnimations; ++i) {
      var animation = gltf.Animations[i];
      Logger.Info($"Animation: {animation.Name}");
      var skeletalAnimation = new SkeletalAnimation(animation.Name);

      int samplersCount = gltf.Samplers.Length;
      skeletalAnimation.Samplers = new SkeletalAnimation.Sampler[samplersCount];
      for (int samplerIndex = 0; samplerIndex < samplersCount; ++samplerIndex) {
        var gltfSampler = gltf.Animations[i].Samplers[samplerIndex];

        skeletalAnimation.Samplers[samplerIndex].Interpolation = SkeletalAnimation.InterpolationMethod.Linear;
        if (gltfSampler.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.STEP) {
          skeletalAnimation.Samplers[samplerIndex].Interpolation = SkeletalAnimation.InterpolationMethod.Step;
        } else if (gltfSampler.Interpolation == glTFLoader.Schema.AnimationSampler.InterpolationEnum.CUBICSPLINE) {
          skeletalAnimation.Samplers[samplerIndex].Interpolation = SkeletalAnimation.InterpolationMethod.CubicSpline;
        }

        // get timestamp
        {
          int count = 0;
          var acc = model.LogicalAccessors[gltfSampler.Input].AsScalarArray();
          count = acc.Count;
          skeletalAnimation.Samplers[samplerIndex].Timestamps = new float[count];
          for (int index = 0; index < count; ++index) {
            skeletalAnimation.Samplers[samplerIndex].Timestamps[index] = acc[index];
          }
        }


        // read sampler keyframes
        {
          var acc = model.LogicalAccessors[gltfSampler.Output].AsVector4Array();
          int count = acc.Count;
          skeletalAnimation.Samplers[samplerIndex].TRSOutputValuesToBeInterpolated = new Vector4[count];
          for (int index = 0; index < count; index++) {
            skeletalAnimation.Samplers[samplerIndex].TRSOutputValuesToBeInterpolated[index] = acc[index];
          }
        }

        if (skeletalAnimation.Samplers.Length > 0) {
          var sampler = skeletalAnimation.Samplers[samplerIndex];
          if (sampler.Timestamps.Length >= 2) {
            skeletalAnimation.SetFirstKeyFrameTime(sampler.Timestamps[0]);
            skeletalAnimation.SetLastKeyFrameTime(sampler.Timestamps.Last());
          }

          var channelsCount = animation.Channels.Length;
          skeletalAnimation.Channels = new SkeletalAnimation.Channel[channelsCount];
          for (int channelIndex = 0; channelIndex < channelsCount; ++channelIndex) {
            var gltfChannel = animation.Channels[channelIndex];

            skeletalAnimation.Channels[channelIndex].SamplerIndex = gltfChannel.Sampler;
            skeletalAnimation.Channels[channelIndex].Node = (int)gltfChannel.Target.Node;

            if (gltfChannel.Target.Path == AnimationChannelTarget.PathEnum.translation) {
              skeletalAnimation.Channels[channelIndex].Path = SkeletalAnimation.Path.Translation;
            } else if (gltfChannel.Target.Path == AnimationChannelTarget.PathEnum.rotation) {
              skeletalAnimation.Channels[channelIndex].Path = SkeletalAnimation.Path.Rotation;
            } else if (gltfChannel.Target.Path == AnimationChannelTarget.PathEnum.scale) {
              skeletalAnimation.Channels[channelIndex].Path = SkeletalAnimation.Path.Scale;
            } else {
              Logger.Error($"[Skin::Init] Path not supported");
            }
          }

          Animations.Add(skeletalAnimation);
          SkeletonAnimations.AddAnimation(skeletalAnimation);
        }
      }
    }
  }

  public void Init(Gltf gltf, byte[] globalBuffer) {
    var gltfSkin = gltf.Skins[0];

    if (gltfSkin.InverseBindMatrices.HasValue) {
      var numofJoints = gltfSkin.Joints.Length;
      Skeleton.JointsData = new JointData[numofJoints];
      Skeleton.FinalJointMatrices = new Matrix4x4[numofJoints];
      Skeleton.Name = gltfSkin.Name;

      var inverseBindMatrices = GLTFLoaderKHR.GetInverseBindMatrices(gltf, globalBuffer, gltfSkin);
      for (int jointIdx = 0; jointIdx < numofJoints; jointIdx++) {
        int globalIdx = gltfSkin.Joints[jointIdx];
        Skeleton.JointsData[jointIdx] = new JointData {
          InverseBindMatrix = inverseBindMatrices[jointIdx],
          Name = gltf.Nodes[globalIdx].Name,
          Node = gltf.Nodes[globalIdx],
        };

        Skeleton.GlobalNodeToJointIdx[globalIdx] = jointIdx;
      }

      int rootJoint = gltfSkin.Joints[0];
      LoadJoint(gltf, rootJoint, Skeleton.NO_PARENT);
    }

    Ssbo = new DwarfBuffer(
      Application.Instance.Device,
      (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)Skeleton.JointsData.Length,
      BufferUsage.StorageBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );
    Ssbo.Map();

    // Load Animations
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
          GLTFLoaderKHR.LoadAccessor<Vector4>(gltf, globalBuffer, acc, out var outArray);
          var vec4Array = outArray.ToVector4Array();
          int count = acc.Count;
          animation.Samplers[samplerIndex].TRSOutputValuesToBeInterpolated = new Vector4[count];
          for (int index = 0; index < count; index++) {
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
      var test = samplersCount;
      Logger.Info("test");

    }
  }

  public void LoadJoint(Gltf gltf, int globalJointIdx, int parent) {
    var current = Skeleton.GlobalNodeToJointIdx[globalJointIdx];
    Skeleton.JointsData[current].ParentJoint = parent;

    if (gltf.Nodes[globalJointIdx].Children == null) {
      Skeleton.JointsData[current].Children = [];
    } else {
      var childCount = gltf.Nodes[globalJointIdx].Children.Length;
      if (childCount > 0) {
        Skeleton.JointsData[current].Children = new int[childCount];
        var children = gltf.Nodes[globalJointIdx].Children;
        for (int i = 0; i < childCount; i++) {
          var globalGltfNodeIdxForChild = children[i];
          Skeleton.JointsData[current].Children[i] = Skeleton.GlobalNodeToJointIdx[globalGltfNodeIdxForChild];
          LoadJoint(gltf, globalGltfNodeIdxForChild, current);
        }
      }
    }
  }

  public void LoadJoint_Old(int globalJointIdx, int parent) {
    var current = Skeleton.GlobalNodeToJointIdx[globalJointIdx];

    Skeleton.JointsData[current].ParentJoint = parent;
    var childCount = GltfModel.LogicalNodes[globalJointIdx].VisualChildren.Count();
    if (childCount > 0) {
      Skeleton.JointsData[current].Children = new int[childCount];
      var children = GltfModel.LogicalNodes[globalJointIdx].VisualChildren.ToArray();
      for (int i = 0; i < childCount; ++i) {
        var globalGltfNodeIdxForChild = children[i].LogicalIndex;
        Skeleton.JointsData[current].Children[i] = Skeleton.GlobalNodeToJointIdx[globalGltfNodeIdxForChild];
        LoadJoint_Old(globalGltfNodeIdxForChild, current);
      }
    }
  }

  public void BuildDescriptor(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    unsafe {
      var range = Ssbo.GetDescriptorBufferInfo(Ssbo.GetAlignmentSize());
      range.range = Ssbo.GetAlignmentSize();

      _ = new VulkanDescriptorWriter(descriptorSetLayout, descriptorPool)
      .WriteBuffer(0, &range)
      .Build(out _descriptorSet);
    }
  }
  public unsafe void Write(nint data) {
    Ssbo.Map();
    Ssbo.WriteToBuffer(data, (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)InverseBindMatrices.Length);
    Ssbo.Unmap();
  }

  public unsafe void Write() {
    fixed (Matrix4x4* ibmPtr = InverseBindMatrices) {
      Ssbo.WriteToBuffer((nint)ibmPtr, Ssbo.GetAlignmentSize());
    }
  }

  public unsafe void WriteSkeleton() {
    fixed (Matrix4x4* ibmPtr = Skeleton.FinalJointMatrices) {
      Ssbo.WriteToBuffer((nint)ibmPtr, Ssbo.GetAlignmentSize());
    }
    Ssbo.Flush();
  }

  public unsafe void WriteSkeletonIdentity() {
    if (Skeleton.FinalJointMatrices.Length < 1) {
      WriteIdentity();
      return;
    }
    var mats = new Matrix4x4[Skeleton.FinalJointMatrices.Length];
    for (int i = 0; i < mats.Length; i++) {
      mats[i] = Matrix4x4.Identity;
    }
    fixed (Matrix4x4* matsPtr = mats) {
      Ssbo.WriteToBuffer((nint)matsPtr, Ssbo.GetAlignmentSize());
    }
  }

  public unsafe void WriteIdentity() {
    var mats = new Matrix4x4[InverseBindMatrices.Length];
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

  public class Builder {
    private readonly Skin _skin = new();

    public Builder SetName(string name) {
      _skin.Name = name;
      return this;
    }

    public Builder SetSkeletonRoot(SharpGLTF.Schema2.Node node) {
      _skin.SkeletonRoot = node;
      return this;
    }

    public Builder SetInverseBindMatrices(Matrix4x4[] inverseBindMatrices) {
      _skin.InverseBindMatrices = inverseBindMatrices;
      return this;
    }

    public Builder SetJoints(IList<SharpGLTF.Schema2.Node> joints) {
      _skin.Joints = joints;
      return this;
    }

    public Skin Build(IDevice device) {
      _skin.Ssbo = new DwarfBuffer(
        device,
        (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)_skin.InverseBindMatrices.Length,
        BufferUsage.StorageBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );

      return _skin;
    }
  }

  public void Dispose() {
    Ssbo?.Dispose();
  }

  public VkDescriptorSet DescriptorSet => _descriptorSet;
  public Matrix4x4[] InverseBindMatrices { get; set; } = [];
}
