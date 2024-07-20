using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Model.Animation;
public class Skin : IDisposable {
  public string Name { get; private set; } = default!;
  public SharpGLTF.Schema2.Node SkeletonRoot { get; private set; } = null!;
  public IList<SharpGLTF.Schema2.Node> Joints { get; private set; } = [];
  public DwarfBuffer Ssbo = null!;
  private VkDescriptorSet _descriptorSet = VkDescriptorSet.Null;

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

  public unsafe void Write(nint data, ulong size, ulong offset) {
    Ssbo.WriteToBuffer(data, size, offset);
  }

  public unsafe void Write(Matrix4x4 data, ulong size, ulong offset) {
    Ssbo.WriteToBuffer((nint)(&data), size, offset);
  }

  public class Builder {
    private readonly Skin _skin = new Skin();

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
  public Matrix4x4[] InverseBindMatrices { get; private set; } = [];
}
