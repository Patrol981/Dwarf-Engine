using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public class Render3DSystem : SystemBase, IRenderSystem {
  private PublicList<PublicList<VkDescriptorSet>> _textureSets = new();
  private Vulkan.Buffer _modelBuffer = null!;

  private VkDescriptorSet _dynamicSet = VkDescriptorSet.Null;
  private DescriptorWriter _dynamicWriter = null!;

  public Render3DSystem(
    Device device,
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
    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(renderer.GetSwapchainRenderPass());
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
      var nid = textures.GetTextureId("./Textures/base/no_texture.png");
      texture = textures.GetTexture(nid);
    }
    VkDescriptorImageInfo imageInfo = new() {
      sampler = texture.GetSampler(),
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = texture.GetImageView()
    };
    VkDescriptorSet set;
    unsafe {
      _ = new DescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out set);
    }

    _textureSets.GetAt(index).SetAt(set, modelPart);
  }

  public void SetupRenderData(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    var startTime = DateTime.Now;
    // TODO: Reuse data from diffrent renders?

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 3D");

    _descriptorPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(1000)
      .AddPoolSize(VkDescriptorType.UniformBufferDynamic, 1000)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = CalculateLengthOfPool(entities);

    _texturePool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)_texturesCount)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _textureSets = new();

    for (int x = 0; x < entities.Length; x++) {
      var en = entities[x].GetDrawable<IRender3DElement>() as IRender3DElement;
      _textureSets.Add(new());
      for (int y = 0; y < en!.MeshsesCount; y++) {
        _textureSets.GetAt(x).Add(new());
      }
    }

    _modelBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
      (ulong)entities.Length,
      VkBufferUsageFlags.UniformBuffer,
      VkMemoryPropertyFlags.HostVisible,
      _device.Properties.limits.minUniformBufferOffsetAlignment
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
      _dynamicWriter = new DescriptorWriter(_setLayout, _descriptorPool);
      _dynamicWriter.WriteBuffer(0, &range);
      _dynamicWriter.Build(out _dynamicSet);

    }

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

  public void RenderEntities(FrameInfo frameInfo, Entity[] entities) {
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

    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].GetDrawable<IRender3DElement>() is not IRender3DElement targetEntity) continue;

      var modelUBO = new ModelUniformBufferObject {
        Material = entities[i].GetComponent<Material>().GetColor()
      };
      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * (uint)i;

      unsafe {
        var fixedSize = _modelBuffer.GetAlignmentSize() / 2;

        var offset = fixedSize * (ulong)(i);
        _modelBuffer.WriteToBuffer((IntPtr)(&modelUBO), _modelBuffer.GetInstanceSize(), offset);
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
          targetEntity.BindDescriptorSet(_textureSets.GetAt(i).GetAt((int)x), frameInfo, ref _pipelineLayout);
          targetEntity.Bind(frameInfo.CommandBuffer, x);
          targetEntity.Draw(frameInfo.CommandBuffer, x);
        }
      }
    }

    _modelBuffer.Unmap();
  }

  private void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPushConstantRange pushConstantRange = new() {
      stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      offset = 0,
      size = (uint)Unsafe.SizeOf<SimplePushConstantData>()
    };

    VkPipelineLayoutCreateInfo pipelineInfo = new() {
      setLayoutCount = (uint)layouts.Length
    };
    unsafe {
      fixed (VkDescriptorSetLayout* ptr = layouts) {
        pipelineInfo.pSetLayouts = ptr;
      }
      pipelineInfo.pushConstantRangeCount = 1;
      pipelineInfo.pPushConstantRanges = &pushConstantRange;
      vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
    }
  }

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    _pipelineConfigInfo ??= new PipelineConfigInfo();
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "vertex", "fragment", pipelineConfig, new PipelineModelProvider());
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    _modelBuffer?.Dispose();
    _descriptorPool?.FreeDescriptors([_dynamicSet]);
    _descriptorPool?.Dispose();
    _texturePool?.FreeDescriptors(_textureSets);
    _texturePool?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}