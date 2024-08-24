using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Model.Animation;
using Dwarf.Rendering;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Model;

public class Node {
  public const int MAX_NUM_JOINTS = 128;

  public Node? Parent;
  public int Index = 0;
  public List<Node> Children = [];
  public Matrix4x4 NodeMatrix = Matrix4x4.Identity;
  public string Name = string.Empty;
  public Mesh? Mesh;
  public Skin? Skin;
  public int SkinIndex = -1;
  public Vector3 Translation = Vector3.Zero;
  public Quaternion Rotation = Quaternion.Identity;
  public Vector3 Scale = Vector3.One;
  public bool UseCachedMatrix = false;
  public Matrix4x4 CachedLocalMatrix = Matrix4x4.Identity;
  public Matrix4x4 CachedMatrix = Matrix4x4.Identity;

  public glTFLoader.Schema.Node? GltfNodeReference;
  public MeshRenderer ParentRenderer = null!;

  public DwarfBuffer Ssbo = null!;
  private VkDescriptorSet _descriptorSet = VkDescriptorSet.Null;

  public float AnimationTimer = 0.0f;

  public void CreateBuffer() {
    Ssbo = new DwarfBuffer(
      Application.Instance.Device,
      (ulong)8192,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );
    Ssbo.Map((ulong)8192);
  }

  public void BuildDescriptor(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    unsafe {
      var targetSize = (ulong)8192;
      var range = Ssbo.GetDescriptorBufferInfo(targetSize);
      range.range = targetSize;

      _ = new VulkanDescriptorWriter(descriptorSetLayout, descriptorPool)
      .WriteBuffer(0, &range)
      .Build(out _descriptorSet);
    }
  }

  public unsafe void WriteSkeleton() {
    fixed (Matrix4x4* matrices = new Matrix4x4[128]) {
      for (int i = 0; i < Skin!.OutputNodeMatrices.Length; i++) {
        matrices[i] = Skin.OutputNodeMatrices[i];
      }
      for (int i = Skin.OutputNodeMatrices.Length; i < 128; i++) {
        matrices[i] = Matrix4x4.Identity;
      }
      Ssbo.WriteToBuffer((IntPtr)matrices, 8192);
    }
  }

  public Matrix4x4 GetLocalMatrix() {
    if (!UseCachedMatrix) {
      CachedLocalMatrix =
        Matrix4x4.CreateTranslation(Translation) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateScale(Scale) * NodeMatrix;
    }
    return CachedLocalMatrix;
  }

  public Matrix4x4 GetMatrix() {
    if (!UseCachedMatrix) {
      Matrix4x4 m = GetLocalMatrix();
      var p = Parent;
      while (p != null) {
        m = p.GetLocalMatrix() * m;
        p = p.Parent;
      }
      CachedMatrix = m;
      UseCachedMatrix = true;
      return m;
    } else {
      return CachedMatrix;
    }
  }

  public Task DrawNode(IntPtr commandBuffer, uint firstInstance = 0) {
    if (!HasMesh) return Task.CompletedTask;

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.DrawIndexed(commandBuffer, Mesh!.IndexCount, 1, 0, 0, firstInstance);
    } else {
      ParentRenderer.Renderer.CommandList.Draw(commandBuffer, Mesh!.VertexCount, 1, 0, firstInstance);
    }
    return Task.CompletedTask;
  }

  public Task BindNode(IntPtr commandBuffer) {
    if (!HasMesh) return Task.CompletedTask;

    ParentRenderer.Renderer.CommandList.BindVertex(commandBuffer, Mesh!.VertexBuffer!, 0);

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.BindIndex(commandBuffer, Mesh!.IndexBuffer!);
    }

    return Task.CompletedTask;
  }

  public void Update() {
    UseCachedMatrix = false;
    if (Mesh != null) {
      Matrix4x4 m = GetMatrix();
      if (Skin != null) {
        Mesh.Matrix = m;
        Matrix4x4.Invert(m, out var inTransform);
        int numJoints = (int)MathF.Min(Skin.Joints.Count, MAX_NUM_JOINTS);
        for (int i = 0; i < numJoints; i++) {
          var jointNode = Skin.Joints[i];
          var jointMat = jointNode.GetMatrix() * Skin.InverseBindMatrices[i];
          jointMat = inTransform * jointMat;
          Skin.OutputNodeMatrices[i] = jointMat;
        }
        Skin.JointsCount = numJoints;
        WriteSkeleton();
      } else {
        Mesh.Matrix = m;
      }
    }

    foreach (var child in Children) {
      child.Update();
    }
  }

  public bool HasMesh => Mesh != null;
  public bool HasSkin => Skin != null;
  public VkDescriptorSet DescriptorSet => _descriptorSet;

  public void Dispose() {
    Skin?.Dispose();
    Mesh?.Dispose();
    Ssbo?.Dispose();
  }
}