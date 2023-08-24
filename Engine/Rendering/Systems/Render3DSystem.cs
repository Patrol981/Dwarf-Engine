using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using DwarfEngine.Engine;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public unsafe class Render3DSystem : SystemBase, IRenderSystem {
  private PublicList<PublicList<VkDescriptorSet>> _textureSets = new();

  public Render3DSystem(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
    .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = new VkDescriptorSetLayout[] {
      globalSetLayout,
      _setLayout.GetDescriptorSetLayout(),
      _textureSetLayout.GetDescriptorSetLayout()
    };
    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(renderer.GetSwapchainRenderPass());
  }

  private int GetLengthOfTexturedEntites(ReadOnlySpan<Entity> entities) {
    int count = 0;
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].GetComponent<Model>().UsesTexture) {
        count++;
      }
    }
    return count;
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
    var id = entity.GetComponent<Model>().GetTextureIdReference(modelPart);
    var texture = textures.GetTexture(id);
    if (texture == null) {
      var nid = textures.GetTextureId("./Textures/base/no_texture.png");
      texture = textures.GetTexture(nid);
    }
    VkDescriptorImageInfo imageInfo = new();
    imageInfo.sampler = texture.GetSampler();
    imageInfo.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
    imageInfo.imageView = texture.GetImageView();
    VkDescriptorSet set;
    var texWriter = new DescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out set);

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

    var lastFrameModels = _buffer;
    var lastFrameSets = _descriptorSets;
    var lastFrameTextures = _textureSets;

    _descriptorPool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)entities.Length)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)entities.Length)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = CalculateLengthOfPool(entities);

    _texturePool = new DescriptorPool.Builder(_device)
    .SetMaxSets((uint)_texturesCount)
    .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
    .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
    .Build();

    _buffer = new Vulkan.Buffer[entities.Length];
    _descriptorSets = new VkDescriptorSet[entities.Length];
    _textureSets = new();

    for (int x = 0; x < entities.Length; x++) {
      var en = entities[x].GetComponent<Model>();
      _textureSets.Add(new());
      for (int y = 0; y < en.MeshsesCount; y++) {
        _textureSets.GetAt(x).Add(new());
      }
    }

    for (int i = 0; i < lastFrameModels.Length; i++) {
      _buffer[i] = lastFrameModels[i];
    }

    for (int i = lastFrameModels.Length; i < entities.Length; i++) {
      _buffer[i] = new Vulkan.Buffer(
        _device,
        (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
        1,
        VkBufferUsageFlags.UniformBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );

      var targetModel = entities[i].GetComponent<Model>();
      if (targetModel.MeshsesCount > 1 && targetModel.UsesTexture) {
        for (int x = 0; x < targetModel.MeshsesCount; x++) {
          BindDescriptorTexture(targetModel.Owner!, ref textures, i, x);
        }
      } else {
        BindDescriptorTexture(targetModel.Owner!, ref textures, i, 0);
      }

      var bufferInfo = _buffer[i].GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<ModelUniformBufferObject>());
      var writer = new DescriptorWriter(_setLayout, _descriptorPool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
    }

    var endTime = DateTime.Now;
    Logger.Warn($"[RENDER 3D RELOAD TIME]: {(endTime - startTime).TotalMilliseconds}");
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (entities.Length > _buffer.Length) {
      return false;
    } else if (entities.Length < _buffer.Length) {
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

  public void RenderEntities(FrameInfo frameInfo, Span<Entity> entities) {
    _pipeline.Bind(frameInfo.CommandBuffer);

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      _pipelineLayout,
      0,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    for (int i = 0; i < entities.Length; i++) {
      var targetEntity = entities[i].GetDrawable<IRender3DElement>() as IRender3DElement;

      var modelUBO = new ModelUniformBufferObject();

      modelUBO.UseLight = targetEntity!.UsesLight;
      modelUBO.UseTexture = targetEntity!.UsesTexture;
      modelUBO.Material = entities[i].GetComponent<Material>().GetColor();

      _buffer[i].Map((ulong)Unsafe.SizeOf<ModelUniformBufferObject>());
      _buffer[i].WriteToBuffer((IntPtr)(&modelUBO), (ulong)Unsafe.SizeOf<ModelUniformBufferObject>());
      _buffer[i].Unmap();

      var pushConstantData = new SimplePushConstantData();
      pushConstantData.ModelMatrix = entities[i].GetComponent<Transform>().Matrix4;
      pushConstantData.NormalMatrix = entities[i].GetComponent<Transform>().NormalMatrix;

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<SimplePushConstantData>(),
        &pushConstantData
      );

      fixed (VkDescriptorSet* ptr = &_descriptorSets[i]) {
        vkCmdBindDescriptorSets(
          frameInfo.CommandBuffer,
          VkPipelineBindPoint.Graphics,
          _pipelineLayout,
          1,
          1,
          ptr,
          0,
          null
        );
      }

      if (!entities[i].CanBeDisposed) {
        for (uint x = 0; x < targetEntity.MeshsesCount; x++) {
          if (!targetEntity.FinishedInitialization) continue;
          if (targetEntity.UsesTexture) {
            targetEntity.BindDescriptorSet(_textureSets.GetAt(i).GetAt((int)x), frameInfo, ref _pipelineLayout);
          }
          targetEntity.Bind(frameInfo.CommandBuffer, x);
          targetEntity.Draw(frameInfo.CommandBuffer, x);
        }
      }
    }
  }

  private void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<SimplePushConstantData>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.setLayoutCount = (uint)layouts.Length;
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    if (_pipelineConfigInfo == null) {
      _pipelineConfigInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "vertex", "fragment", pipelineConfig, new PipelineModelProvider());
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    for (int i = 0; i < _buffer.Length; i++) {
      _buffer[i]?.Dispose();
    }
    _descriptorPool?.FreeDescriptors(_descriptorSets);
    _descriptorPool?.Dispose();
    _texturePool?.FreeDescriptors(_textureSets);
    _texturePool?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}