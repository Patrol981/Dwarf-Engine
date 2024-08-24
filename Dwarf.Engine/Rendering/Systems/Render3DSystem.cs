using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Model;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class Render3DSystem : SystemBase, IRenderSystem {
  public const string Simple3D = "simple3D";
  public const string Skinned3D = "skinned3D";

  private DwarfBuffer _modelBuffer = null!;

  private VkDescriptorSet _dynamicSet = VkDescriptorSet.Null;
  private VulkanDescriptorWriter _dynamicWriter = null!;

  private readonly DescriptorSetLayout _jointDescriptorLayout = null!;

  // private readonly List<VkDrawIndexedIndirectCommand> _indirectCommands = [];
  // private readonly DwarfBuffer _indirectCommandBuffer = null!;

  // private ModelUniformBufferObject _modelUbo = new();
  private readonly unsafe ModelUniformBufferObject* _modelUbo =
    (ModelUniformBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<ModelUniformBufferObject>());
  private readonly unsafe SimplePushConstantData* _pushConstantData =
    (SimplePushConstantData*)Marshal.AllocHGlobal(Unsafe.SizeOf<SimplePushConstantData>());

  private IRender3DElement[] _notSkinnedEntitiesCache = [];
  private IRender3DElement[] _skinnedEntitiesCache = [];

  private Node[] _notSkinnedNodesCache = [];
  private Node[] _skinnedNodesCache = [];

  public Render3DSystem(
    IDevice device,
    Renderer renderer,
    // VkDescriptorSetLayout[] externalLayouts,
    Dictionary<string, DescriptorSetLayout> externalLayouts,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBufferDynamic, VkShaderStageFlags.AllGraphics)
      .Build();

    _jointDescriptorLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
    .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    .Build();

    VkDescriptorSetLayout[] basicLayouts = [
      _textureSetLayout.GetDescriptorSetLayout(),
      externalLayouts["Global"].GetDescriptorSetLayout(),
      externalLayouts["ObjectData"].GetDescriptorSetLayout(),
      _setLayout.GetDescriptorSetLayout(),
      externalLayouts["PointLight"].GetDescriptorSetLayout(),
    ];

    VkDescriptorSetLayout[] complexLayouts = [
      .. basicLayouts,
      _jointDescriptorLayout.GetDescriptorSetLayout(),
    ];

    AddPipelineData<SimplePushConstantData>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "vertex",
      FragmentName = "fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = basicLayouts,
      PipelineName = Simple3D
    });

    AddPipelineData<SimplePushConstantData>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "vertex_skinned",
      FragmentName = "fragment_skinned",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = complexLayouts,
      PipelineName = Skinned3D
    });
  }

  private int CalculateLengthOfPool(ReadOnlySpan<Entity> entities) {
    int count = 0;
    for (int i = 0; i < entities.Length; i++) {
      var targetItems = entities[i].GetDrawables<IRender3DElement>();
      for (int j = 0; j < targetItems.Length; j++) {
        var t = targetItems[j] as IRender3DElement;
        count += t!.MeshedNodesCount;
      }

    }
    return count;
  }

  private void BuildTargetDescriptorTexture(Entity entity, ref TextureManager textures, int modelPart = 0) {
    var target = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
    var id = target!.GetTextureIdReference(modelPart);
    var texture = (VulkanTexture)textures.GetTexture(id);
    if (texture == null) {
      var nid = textures.GetTextureId("./Resources/Textures/base/no_texture.png");
      texture = (VulkanTexture)textures.GetTexture(nid);
    }

    texture.BuildDescriptor(_textureSetLayout, _descriptorPool);
  }

  private void BuildTargetDescriptorTexture(IRender3DElement target, ref TextureManager textureManager) {
    for (int i = 0; i < target.MeshedNodesCount; i++) {
      var textureId = target.GetTextureIdReference();
      var texture = (VulkanTexture)textureManager.GetTexture(textureId);
      texture.BuildDescriptor(_textureSetLayout, _descriptorPool);
    }
  }

  private void BuildTargetDescriptorJointBuffer(Node node) {
    if (node.Skin == null) return;

    node.BuildDescriptor(_jointDescriptorLayout, _descriptorPool);
  }

  private void BuildTargetDescriptorJointBuffer(IRender3DElement target) {
    for (int i = 0; i < target.MeshedNodesCount; i++) {
      if (!target.MeshedNodes[i].HasSkin) return;

      target.MeshedNodes[i].BuildDescriptor(_jointDescriptorLayout, _descriptorPool);
    }
  }
  private int CalculateNodesLength(ReadOnlySpan<Entity> entities) {
    int len = 0;
    foreach (var entity in entities) {
      var i3d = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
      len += i3d!.MeshedNodesCount;
    }
    return len;
  }

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    _device.WaitQueue();
    var startTime = DateTime.Now;

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 3D");

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(3000)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 1000)
      .AddPoolSize(VkDescriptorType.UniformBufferDynamic, 1000)
      .AddPoolSize(VkDescriptorType.UniformBuffer, 1000)
      .AddPoolSize(VkDescriptorType.StorageBuffer, 1000)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = CalculateLengthOfPool(entities);

    // entities.length before, param no.3
    _modelBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
      (ulong)_texturesCount,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
    );

    // LastKnownElemCount = entities.Length;
    LastKnownElemCount = CalculateNodesLength(entities);

    for (int i = 0; i < entities.Length; i++) {
      var targetModel = entities[i].GetDrawable<IRender3DElement>() as IRender3DElement;
      BuildTargetDescriptorTexture(targetModel!, ref textures);
      BuildTargetDescriptorJointBuffer(targetModel!);
    }

    var range = _modelBuffer.GetDescriptorBufferInfo(_modelBuffer.GetAlignmentSize());
    range.range = _modelBuffer.GetAlignmentSize();
    unsafe {
      _dynamicWriter = new VulkanDescriptorWriter(_setLayout, _descriptorPool);
      _dynamicWriter.WriteBuffer(0, &range);
      _dynamicWriter.Build(out _dynamicSet);
    }

    var endTime = DateTime.Now;
    Logger.Warn($"[RENDER 3D RELOAD TIME]: {(endTime - startTime).TotalMilliseconds}");
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (_modelBuffer == null) {
      var textureManager = Application.Instance.TextureManager;
      Setup(entities, ref textureManager);
    }
    if (entities.Length > (int)_modelBuffer!.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (int)_modelBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = CalculateLengthOfPool(entities);
    return len == _texturesCount;
  }

  public void Update(Span<IRender3DElement> entities, out ObjectData[] objectData) {
    if (entities.Length < 1) {
      objectData = [];
      return;
    }

    List<Node> skinnedNodes = [];
    List<Node> notSkinnedNodes = [];

    List<ObjectData> objectDataSkinned = [];
    List<ObjectData> objectDataNotSkinned = [];

    foreach (var entity in entities) {
      var transform = entity.GetOwner().GetComponent<Transform>();
      foreach (var node in entity.MeshedNodes) {
        if (node.HasSkin) {
          skinnedNodes.Add(node);
          objectDataSkinned.Add(new ObjectData {
            ModelMatrix = transform.Matrix4,
            NormalMatrix = transform.NormalMatrix,
            NodeMatrix = node.Mesh!.Matrix,
          });
        } else {
          notSkinnedNodes.Add(node);
          objectDataNotSkinned.Add(new ObjectData {
            ModelMatrix = transform.Matrix4,
            NormalMatrix = transform.NormalMatrix,
            NodeMatrix = node.Mesh!.Matrix,
          });
        }
      }
    }

    _skinnedNodesCache = [.. skinnedNodes];
    _notSkinnedNodesCache = [.. notSkinnedNodes];

    objectData = [.. objectDataNotSkinned, .. objectDataSkinned];

    Guizmos.Clear();
  }

  public void Update_Old(Span<IRender3DElement> entities, out ObjectData[] objectData) {
    if (entities.Length < 1) {
      objectData = [];
      return;
    }

    var skinnedEntities = entities.ToArray().Where(x => x.IsSkinned).ToArray();
    var notSkinnedEntities = entities.ToArray().Where(x => !x.IsSkinned).ToArray();

    var notSkinnedArray = new ObjectData[notSkinnedEntities.Length];
    var skinnedArray = new ObjectData[skinnedEntities.Length];
    objectData = new ObjectData[entities.Length];

    for (int i = 0; i < notSkinnedEntities.Length; i++) {
      var transform = notSkinnedEntities[i].GetOwner().GetComponent<Transform>();
      notSkinnedArray[i] = new ObjectData {
        ModelMatrix = transform.Matrix4,
        NormalMatrix = transform.NormalMatrix
      };
      // objectData[i].IsSkinned = 0;
      // Logger.Info($"{notSkinnedEntities[i].GetOwner().Name} {transform.Position} {notSkinnedArray[i].ModelMatrix.Translation}");
    }
    for (int i = 0; i < skinnedEntities.Length; i++) {
      var transform = skinnedEntities[i].GetOwner().GetComponent<Transform>();
      skinnedArray[i] = new ObjectData {
        ModelMatrix = transform.Matrix4,
        NormalMatrix = transform.NormalMatrix,
        // NodeMatrix = skinnedEntities[i].
        // IsSkinned = 1
      };
      // Logger.Info($"{skinnedEntities[i].GetOwner().Name} {transform.Position} {skinnedArray[i].ModelMatrix.Translation}");
    }

    _notSkinnedEntitiesCache = notSkinnedEntities;
    _skinnedEntitiesCache = skinnedEntities;

    objectData = [.. notSkinnedArray, .. skinnedArray];
  }

  public void Render(FrameInfo frameInfo, Span<IRender3DElement> entities) {
    // if (entities.Length < 1) return;

    // var skinnedEntities = entities.ToArray().Where(x => x.IsSkinned);
    // var notSkinnedEntities = entities.ToArray().Where(x => !x.IsSkinned);

    // RenderSimple(frameInfo, _notSkinnedEntitiesCache);
    // RenderComplex(frameInfo, _skinnedEntitiesCache, _notSkinnedEntitiesCache.Length);

    RenderSimple(frameInfo, _notSkinnedNodesCache);
    RenderComplex(frameInfo, _skinnedNodesCache, _notSkinnedNodesCache.Length);
  }

  private void RenderSimple(FrameInfo frameInfo, Span<Node> nodes) {
    BindPipeline(frameInfo.CommandBuffer, Simple3D);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Simple3D].PipelineLayout,
        1,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Simple3D].PipelineLayout,
        4,
        1,
        &frameInfo.PointLightsDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Simple3D].PipelineLayout,
        2,
        1,
        &frameInfo.ObjectDataDescriptorSet,
        0,
        null
      );
    }

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      var materialData = nodes[i].ParentRenderer.GetOwner().GetComponent<Material>().Data;
      unsafe {
        _modelUbo->Color = materialData.Color;
        _modelUbo->Specular = materialData.Specular;
        _modelUbo->Shininess = materialData.Shininess;
        _modelUbo->Diffuse = materialData.Diffuse;
        _modelUbo->Ambient = materialData.Ambient;
      }

      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * (uint)i;

      unsafe {
        _modelBuffer.WriteToBuffer((IntPtr)_modelUbo, _modelBuffer.GetInstanceSize(), dynamicOffset >> 1);
      }

      unsafe {
        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelines[Simple3D].PipelineLayout,
            3,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      if (!nodes[i].ParentRenderer.GetOwner().CanBeDisposed && nodes[i].ParentRenderer.GetOwner().Active) {
        if (i == _texturesCount) continue;
        var targetTexture = frameInfo.TextureManager.GetTexture(nodes[i].Mesh!.TextureIdReference);
        Descriptor.BindDescriptorSet(
          targetTexture.TextureDescriptor,
          frameInfo,
          _pipelines[Simple3D].PipelineLayout,
          0,
          1
        );

        nodes[i].BindNode(frameInfo.CommandBuffer);
        nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i);
      }
    }

    _modelBuffer.Unmap();
  }

  private void RenderComplex(FrameInfo frameInfo, Span<Node> nodes, int prevIdx) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        1,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        4,
        1,
        &frameInfo.PointLightsDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        2,
        1,
        &frameInfo.ObjectDataDescriptorSet,
        0,
        null
      );
    }

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      var materialData = nodes[i].ParentRenderer.GetOwner().GetComponent<Material>().Data;
      unsafe {
        _modelUbo->Color = materialData.Color;
        _modelUbo->Specular = materialData.Specular;
        _modelUbo->Shininess = materialData.Shininess;
        _modelUbo->Diffuse = materialData.Diffuse;
        _modelUbo->Ambient = materialData.Ambient;
      }

      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * ((uint)i + (uint)prevIdx);

      unsafe {
        _modelBuffer.WriteToBuffer((IntPtr)(_modelUbo), _modelBuffer.GetInstanceSize(), dynamicOffset >> 1);
      }

      unsafe {
        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelines[Skinned3D].PipelineLayout,
            3,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      nodes[i].AnimationTimer += Time.DeltaTime * 100;
      if (nodes[i].AnimationTimer > nodes[i].ParentRenderer.Animations[0].End) {
        nodes[i].AnimationTimer -= nodes[i].ParentRenderer.Animations[0].End;
      }
      nodes[i].ParentRenderer.UpdateAnimation(2, nodes[i].AnimationTimer);
      // nodes[i].WriteIdentity();
      Descriptor.BindDescriptorSet(
        nodes[i].DescriptorSet,
        frameInfo,
        _pipelines[Skinned3D].PipelineLayout,
        5,
        1
      );

      var targetTexture = frameInfo.TextureManager.GetTexture(nodes[i].Mesh!.TextureIdReference);
      Descriptor.BindDescriptorSet(targetTexture.TextureDescriptor, frameInfo, PipelineLayout, 0, 1);

      nodes[i].BindNode(frameInfo.CommandBuffer);
      nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i + (uint)prevIdx);
    }

    _modelBuffer.Unmap();
  }


  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    MemoryUtils.FreeIntPtr<SimplePushConstantData>((nint)_pushConstantData);
    MemoryUtils.FreeIntPtr<ModelUniformBufferObject>((nint)_modelUbo);

    _modelBuffer?.Dispose();
    _descriptorPool?.FreeDescriptors([_dynamicSet]);

    _jointDescriptorLayout?.Dispose();

    base.Dispose();
  }

  public IRender3DElement[] CachedRenderables => [.. _notSkinnedEntitiesCache, .. _skinnedEntitiesCache];
  public int LastKnownElemCount { get; private set; }
}