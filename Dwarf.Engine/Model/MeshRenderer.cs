using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Model;
using Dwarf.Model.Animation;
using Dwarf.Physics;
using Dwarf.Rendering;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf;

public class MeshRenderer : Component, IRender3DElement, ICollision {
  private readonly IDevice _device = null!;
  private readonly Renderer _renderer = null!;
  private readonly AABB _mergedAABB = new();

  private VkDescriptorSet _skinDescriptor = VkDescriptorSet.Null;

  public MeshRenderer() { }

  public MeshRenderer(IDevice device, Renderer renderer) {
    _device = device;
    _renderer = renderer;
  }

  public MeshRenderer(IDevice device, Renderer renderer, Node[] nodes, Node[] linearNodes) {
    _device = device;
    _renderer = renderer;
    Init(nodes, linearNodes);
  }

  public MeshRenderer(
    IDevice device,
    Renderer renderer,
    Node[] nodes,
    Node[] linearNodes,
    string fileName
  ) {
    _device = device;
    _renderer = renderer;
    FileName = fileName;
    Init(nodes, linearNodes);
  }

  public void Init() {
    NodesCount = Nodes.Length;
    MeshedNodesCount = LinearNodes.Where(x => x.HasMesh).Count();
    LinearNodesCount = LinearNodes.Length;

    MeshedNodes = LinearNodes.Where(x => x.HasMesh).ToArray();

    InitBase();
  }

  protected void Init(Node[] nodes, Node[] linearNodes) {
    NodesCount = nodes.Length;
    MeshedNodesCount = linearNodes.Where(x => x.HasMesh).Count();
    LinearNodesCount = linearNodes.Length;

    Nodes = nodes;
    LinearNodes = linearNodes;
    MeshedNodes = LinearNodes.Where(x => x.HasMesh).ToArray();

    InitBase();
  }

  private void InitBase() {
    AABBArray = new AABB[MeshedNodesCount];

    List<Task> createTasks = [];

    if (LinearNodesCount < 1) throw new ArgumentOutOfRangeException(nameof(LinearNodesCount));

    for (int i = 0; i < MeshedNodes.Length; i++) {
      if (MeshedNodes[i].HasMesh) {
        createTasks.Add(MeshedNodes[i].Mesh!.CreateVertexBuffer());
        createTasks.Add(MeshedNodes[i].Mesh!.CreateIndexBuffer());

        AABBArray[i] = new();
        AABBArray[i].Update(MeshedNodes[i].Mesh!);
      }
    }

    _mergedAABB.Update(AABBArray);
    RunTasks(createTasks);
  }

  public unsafe ulong CalculateBufferSize() {
    ulong baseSize = (ulong)sizeof(Matrix4x4);
    ulong currentBufferSize = 0;
    foreach (var node in MeshedNodes) {
      if (node.HasSkin) {
        currentBufferSize += ((ulong)node.Skin!.OutputNodeMatrices.Length * baseSize);
      }
    }
    return currentBufferSize;
  }

  protected async void RunTasks(List<Task> createTasks) {
    await Task.WhenAll(createTasks);
    FinishedInitialization = true;
  }

  public Task Bind(IntPtr commandBuffer, uint index) {
    var node = LinearNodes[index];
    if (!node.HasMesh) return Task.CompletedTask;

    _renderer.CommandList.BindVertex(commandBuffer, node.Mesh!.VertexBuffer!, 0);

    if (node.Mesh!.HasIndexBuffer) {
      _renderer.CommandList.BindIndex(commandBuffer, node.Mesh!.IndexBuffer!);
    }

    return Task.CompletedTask;
  }

  public Task Draw(IntPtr commandBuffer, uint index, uint firstInstance = 0) {
    var node = LinearNodes[index];
    if (!node.HasMesh) return Task.CompletedTask;

    if (node.Mesh!.HasIndexBuffer) {
      _renderer.CommandList.DrawIndexed(commandBuffer, node.Mesh!.IndexCount, 1, 0, 0, firstInstance);
    } else {
      _renderer.CommandList.Draw(commandBuffer, node.Mesh!.VertexCount, 1, 0, firstInstance);
    }
    return Task.CompletedTask;
  }

  public void BindToTexture(
    TextureManager textureManager,
    string texturePath,
    int modelPart = 0
  ) {
    MeshedNodes[modelPart]?.Mesh?.BindToTexture(textureManager, texturePath);
  }

  public void BindToTexture(TextureManager textureManager, Guid textureId, int modelPart = 0) {
    MeshedNodes[modelPart]?.Mesh?.BindToTexture(textureManager, textureId);
  }

  public void BindMultipleModelPartsToTexture(TextureManager textureManager, string path) {
    for (int i = 0; i < NodesCount; i++) {
      BindToTexture(textureManager, path, i);
    }
  }

  public void BindMultipleModelPartsToTextures(
    TextureManager textureManager,
    ReadOnlySpan<string> paths
  ) {
    for (int i = 0; i < LinearNodesCount; i++) {
      BindToTexture(textureManager, paths[i], i);
    }
  }

  public void BuildDescriptors(DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool) {
    unsafe {
      var range = Ssbo.GetDescriptorBufferInfo(Ssbo.GetAlignmentSize());
      range.range = Ssbo.GetAlignmentSize();

      _ = new VulkanDescriptorWriter(descriptorSetLayout, descriptorPool)
      .WriteBuffer(0, &range)
      .Build(out _skinDescriptor);
    }
  }

  public unsafe void UpdateAnimation(int idx, float time) {
    if (Animations.Count < 1) {
      Logger.Error($".glTF of {Owner!.Name} does not contain animation.");
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
      for (int i = 0; i < sampler.Inputs.Count - 1; i++) {
        if ((time >= sampler.Inputs[i]) && (time <= sampler.Inputs[i + 1])) {
          float u = MathF.Max(0.0f, time - sampler.Inputs[i]) / (sampler.Inputs[i + 1] - sampler.Inputs[i]);
          if (u <= 1.0f) {
            switch (channel.Path) {
              case AnimationChannel.PathType.Translation:
                sampler.Translate(i, time, channel.Node);
                break;
              case AnimationChannel.PathType.Rotation:
                sampler.Rotate(i, time, channel.Node);
                break;
              case AnimationChannel.PathType.Scale:
                sampler.Scale(i, time, channel.Node);
                break;
            }
            updated = true;
          }
        }
      }
    }
    if (updated) {
      foreach (var node in Nodes) {
        node.Update();
      }
    }
  }

  public unsafe void Dispose() {
    foreach (var node in LinearNodes) {
      _device.WaitQueue();
      _device.WaitDevice();
      node.Dispose();
    }

    Ssbo?.Dispose();
  }

  public int NodesCount { get; private set; } = 0;
  public int MeshedNodesCount { get; private set; } = 0;
  public int LinearNodesCount { get; private set; } = 0;
  public Node[] Nodes { get; private set; } = [];
  public Node[] LinearNodes { get; private set; } = [];
  public Node[] MeshedNodes { get; private set; } = [];

  public List<Animation> Animations = [];
  public List<Skin> Skins = [];

  public DwarfBuffer Ssbo { get; set; } = null!;
  public Matrix4x4[] InverseMatrices { get; set; } = [];


  public VkDescriptorSet SkinDescriptor => _skinDescriptor;
  public DwarfBuffer EntireSkinSSBO { get; private set; } = null!;
  public string FileName { get; } = "";
  public int TextureFlipped { get; set; } = 1;
  public void AddNode(Node node) {
    node.ParentRenderer = this;
    var tmp = Nodes.ToList();
    tmp.Add(node);
    Nodes = [.. tmp];
  }
  public void AddLinearNode(Node node) {
    node.ParentRenderer = this;
    var tmp = LinearNodes.ToList();
    tmp.Add(node);
    LinearNodes = [.. tmp];
  }

  public Node? FindNode(Node parent, int idx) {
    Node? found = null!;
    if (parent.Index == idx) {
      return parent;
    }
    foreach (var child in parent.Children) {
      found = FindNode(child, idx);
      if (found != null) {
        break;
      }
    }
    return found;
  }
  public Node? NodeFromIndex(int idx) {
    Node? found = null!;
    foreach (var node in Nodes) {
      found = FindNode(node, idx);
      if (found != null) {
        break;
      }
    }
    return found;
  }
  public float CalculateHeightOfAnModel() {
    var height = 0.0f;
    foreach (var n in LinearNodes) {
      height += n.Mesh?.Height != null ? n.Mesh.Height : 0;
    }
    return height;
  }
  public Guid GetTextureIdReference(int index = 0) {
    return MeshedNodes[index].Mesh != null ? MeshedNodes[index].Mesh!.TextureIdReference : Guid.Empty;
  }
  public bool FinishedInitialization { get; private set; } = false;

  public bool IsSkinned {
    get {
      // return LinearNodes.Where(x => x.Skin != null).Count() > 0;
      return MeshedNodes.Where(x => x.SkinIndex > -1).Count() > 0;
    }
  }

  public Entity GetOwner() => Owner!;
  public Renderer Renderer => _renderer;

  public AABB[] AABBArray { get; private set; } = [];

  public AABB AABB {
    get {
      return Owner!.HasComponent<ColliderMesh>()
        ? AABB.CalculateOnFlyWithMatrix(Owner!.GetComponent<ColliderMesh>().Mesh, Owner!.GetComponent<Transform>())
        : _mergedAABB;
    }
  }
}