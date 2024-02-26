using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public class Render3DSystem : SystemBase, IRenderSystem {
  private PublicList<PublicList<VkDescriptorSet>> _textureSets = new();
  private DwarfBuffer _modelBuffer = null!;

  private VkDescriptorSet _dynamicSet = VkDescriptorSet.Null;
  private VulkanDescriptorWriter _dynamicWriter = null!;

  private List<VkDrawIndexedIndirectCommand> _indirectCommands = [];
  private DwarfBuffer _indirectCommandBuffer = null!;

  private ModelUniformBufferObject _modelUbo = new();

  public Render3DSystem(
    IDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBufferDynamic, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
    .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      _textureSetLayout.GetDescriptorSetLayout(),
      globalSetLayout,
      _setLayout.GetDescriptorSetLayout()
    ];

    CreatePipelineLayout<SimplePushConstantData>(descriptorSetLayouts);
    CreatePipeline(renderer.GetSwapchainRenderPass(), "vertex", "fragment", new PipelineModelProvider());
  }

  private int CalculateLengthOfPool(ReadOnlySpan<Entity> entities) {
    int count = 0;
    for (int i = 0; i < entities.Length; i++) {
      var targetItems = entities[i].GetDrawables<IRender3DElement>();
      for (int j = 0; j < targetItems.Length; j++) {
        var t = targetItems[j] as IRender3DElement;
        count += t!.MeshsesCount;
      }

    }
    return count;
  }

  private int GetTextureSetsLength() {
    int count = 0;
    for (int i = 0; i < _textureSets.Size; i++) {
      count += _textureSets.GetAt(i).Size;
    }
    return count;
  }

  private void BindDescriptorTexture(Entity entity, ref TextureManager textures, int index, int modelPart = 0) {
    var target = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
    var id = target!.GetTextureIdReference(modelPart);
    var texture = textures.GetTexture(id);
    if (texture == null) {
      var nid = textures.GetTextureId("./Resources/Textures/base/no_texture.png");
      texture = textures.GetTexture(nid);
    }
    VkDescriptorImageInfo imageInfo = new() {
      sampler = texture.GetSampler(),
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = texture.GetImageView()
    };
    VkDescriptorSet set;
    unsafe {
      _ = new VulkanDescriptorWriter(_textureSetLayout, _descriptorPool)
      .WriteImage(0, &imageInfo)
      .Build(out set);
    }

    _textureSets.GetAt(index).SetAt(set, modelPart);
  }

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    // _device.WaitDevice();
    _device.WaitQueue();
    var startTime = DateTime.Now;
    // TODO: Reuse data from diffrent renders?

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 3D");

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(2000)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 1000)
      .AddPoolSize(VkDescriptorType.UniformBufferDynamic, 1000)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = CalculateLengthOfPool(entities);

    _textureSets = new();

    for (int x = 0; x < entities.Length; x++) {
      var en = entities[x].GetDrawable<IRender3DElement>() as IRender3DElement;
      _textureSets.Add(new());
      for (int y = 0; y < en!.MeshsesCount; y++) {
        _textureSets.GetAt(x).Add(new());
      }
    }

    // entities.length before, param no.3
    _modelBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
      (ulong)_texturesCount,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible,
      ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
    );

    for (int i = 0; i < entities.Length; i++) {
      var targetModel = entities[i].GetDrawable<IRender3DElement>() as IRender3DElement;
      if (targetModel!.MeshsesCount > 1) {
        for (int x = 0; x < targetModel.MeshsesCount; x++) {
          BindDescriptorTexture(entities[i], ref textures, i, x);
        }
      } else {
        BindDescriptorTexture(entities[i], ref textures, i, 0);
      }
    }

    var range = _modelBuffer.GetDescriptorBufferInfo(_modelBuffer.GetAlignmentSize());
    range.range = _modelBuffer.GetAlignmentSize();
    unsafe {
      _dynamicWriter = new VulkanDescriptorWriter(_setLayout, _descriptorPool);
      _dynamicWriter.WriteBuffer(0, &range);
      _dynamicWriter.Build(out _dynamicSet);

    }

    // PrepareIndirect(entities);

    var endTime = DateTime.Now;
    Logger.Warn($"[RENDER 3D RELOAD TIME]: {(endTime - startTime).TotalMilliseconds}");
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (entities.Length > (int)_modelBuffer.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (int)_modelBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = CalculateLengthOfPool(entities);
    var sets = GetTextureSetsLength();
    if (len != sets) {
      return false;
    }

    return true;
  }

  public void Render(FrameInfo frameInfo, Span<Entity> entities) {
    if (entities.Length < 1) return;

    _pipeline.Bind(frameInfo.CommandBuffer);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelineLayout,
        1,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );
    }

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    IRender3DElement lastModel = null!;

    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].GetDrawable<IRender3DElement>() is not IRender3DElement targetEntity) continue;
      if (entities[i].CanBeDisposed) continue;

      var materialData = entities[i].GetComponent<Material>().Data;
      /*
      var modelUBO = new ModelUniformBufferObject {
        Material = materialData
      };
      */

      _modelUbo.Material = materialData;
      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * (uint)i;

      ulong offset = 0;
      unsafe {
        var fixedSize = _modelBuffer.GetAlignmentSize() / 2;

        offset = fixedSize * (ulong)(i);
        fixed (ModelUniformBufferObject* modelUboPtr = &_modelUbo) {
          _modelBuffer.WriteToBuffer((IntPtr)(modelUboPtr), _modelBuffer.GetInstanceSize(), offset);
        }
      }

      var transform = entities[i].GetComponent<Transform>();
      var pushConstantData = new SimplePushConstantData {
        ModelMatrix = transform.Matrix4,
        NormalMatrix = transform.NormalMatrix
      };

      unsafe {
        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          _pipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<SimplePushConstantData>(),
          &pushConstantData
        );

        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelineLayout,
            2,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      if (!entities[i].CanBeDisposed && entities[i].Active) {
        for (uint x = 0; x < targetEntity.MeshsesCount; x++) {
          if (!targetEntity.FinishedInitialization) continue;
          // targetEntity.BindDescriptorSet(_textureSets.GetAt(i).GetAt((int)x), frameInfo, ref _pipelineLayout);

          if (i == _textureSets.Size) continue;
          Descriptor.BindDescriptorSet((VulkanDevice)_device, _textureSets.GetAt(i).GetAt((int)x), frameInfo, ref _pipelineLayout, 0, 1);

          if (targetEntity != lastModel)
            targetEntity.Bind(frameInfo.CommandBuffer, x);

          // targetEntity.DrawIndirect(frameInfo.CommandBuffer, _modelBuffer.GetBuffer(), offset, 1,)
          targetEntity.Draw(frameInfo.CommandBuffer, x);
        }
      }

      lastModel = targetEntity;
    }

    /*
    uint inId = 0;
    foreach (var cmd in _indirectCommands) {
      vkCmdDrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectCommandBuffer.GetBuffer(),
        inId * (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>(),
        1,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
      inId++;
    }
    */

    _modelBuffer.Unmap();
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();
    _modelBuffer?.Dispose();
    _descriptorPool?.FreeDescriptors([_dynamicSet]);
    // _texturePool?.FreeDescriptors(_textureSets);

    // _indirectCommandBuffer?.Dispose();

    base.Dispose();
  }
}