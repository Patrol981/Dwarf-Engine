using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using DwarfEngine.Engine;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;
public class Render2DSystem : SystemBase, IRenderSystem {
  private readonly Device _device = null!;
  private readonly Renderer _renderer = null!;
  private PipelineConfigInfo _configInfo = null!;
  private Pipeline _pipeline = null!;
  private VkPipelineLayout _pipelineLayout;

  private Vulkan.Buffer[] _spriteBuffer = new Vulkan.Buffer[0];
  private DescriptorPool _pool = null!;
  private DescriptorPool _texturePool = null!;
  private DescriptorSetLayout _setLayout = null!;
  private DescriptorSetLayout _textureSetLayout = null!;
  private VkDescriptorSet[] _descriptorSets = new VkDescriptorSet[0];
  private PublicList<VkDescriptorSet> _textureSets = new();

  private int _texturesCount = 0;

  public Render2DSystem() { }

  public Render2DSystem(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) {
    _device = device;
    _renderer = renderer;
    _configInfo = configInfo;

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

  public unsafe void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 2D renderer are less than 1, thus 2D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 2D");

    _pool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)entities.Length)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)entities.Length)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = entities.Length;

    _texturePool = new DescriptorPool.Builder(_device)
    .SetMaxSets((uint)_texturesCount)
    .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
    .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
    .Build();

    _spriteBuffer = new Vulkan.Buffer[entities.Length];
    _descriptorSets = new VkDescriptorSet[entities.Length];
    _textureSets = new();

    for (int x = 0; x < entities.Length; x++) {
      _textureSets.Add(new());
    }

    for (int i = 0; i < entities.Length; i++) {
      _spriteBuffer[i] = new Vulkan.Buffer(
        _device,
        (ulong)Unsafe.SizeOf<SpriteUniformBufferObject>(),
        1,
        VkBufferUsageFlags.UniformBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );

      var targetSprite = entities[i].GetComponent<Sprite>();
      if (targetSprite.UsesTexture) {
        BindDescriptorTexture(targetSprite.Owner!, ref textures, i);
      }

      var bufferInfo = _spriteBuffer[i].GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<SpriteUniformBufferObject>());
      var writer = new DescriptorWriter(_setLayout, _pool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
    }
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (entities.Length > _spriteBuffer.Length) {
      return false;
    } else if (entities.Length < _spriteBuffer.Length) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = entities.Length;
    var sets = _textureSets.Size;
    if (len != sets) {
      return false;
    }

    return true;
  }

  public unsafe void RenderEntities(FrameInfo frameInfo, Span<Entity> entities) {
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
      if (!entities[i].Active) continue;

      var spriteUBO = new SpriteUniformBufferObject();
      spriteUBO.SpriteMatrix = entities[i].GetComponent<Transform>().Matrix4;
      spriteUBO.SpriteColor = entities[i].GetComponent<Material>().GetColor();
      spriteUBO.UseTexture = entities[i].GetComponent<Sprite>().UsesTexture;

      // here is performing bad
      // [TODO] restore push constants maybe?

      var pushConstantData = new SpriteUniformBufferObject();
      pushConstantData.SpriteMatrix = entities[i].GetComponent<Transform>().Matrix4;
      pushConstantData.SpriteColor = entities[i].GetComponent<Material>().GetColor();
      pushConstantData.UseTexture = true;

      //_spriteBuffer[i].Map((ulong)Unsafe.SizeOf<SpriteUniformBufferObject>());
      //_spriteBuffer[i].WriteToBuffer((IntPtr)(&spriteUBO), (ulong)Unsafe.SizeOf<SpriteUniformBufferObject>());
      //_spriteBuffer[i].Unmap();



      // Console.WriteLine(spriteUBO.SpriteMatrix);

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<SpriteUniformBufferObject>(),
        &pushConstantData
      );

      /*
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
      */

      var sprite = entities[i].GetComponent<Sprite>();
      if (!sprite.Owner!.CanBeDisposed && sprite.Owner!.Active) {
        if (sprite.UsesTexture)
          sprite.BindDescriptorSet(_textureSets.GetAt(i), frameInfo, ref _pipelineLayout);
        sprite.Bind(frameInfo.CommandBuffer);
        sprite.Draw(frameInfo.CommandBuffer);
      }
    }
  }

  private unsafe void BindDescriptorTexture(Entity entity, ref TextureManager textureManager, int index) {
    var id = entity.GetComponent<Sprite>().GetTextureIdReference();
    var texture = textureManager.GetTexture(id);
    VkDescriptorImageInfo imageInfo = new();
    imageInfo.sampler = texture.GetSampler();
    imageInfo.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
    imageInfo.imageView = texture.GetImageView();
    VkDescriptorSet set;
    var texWriter = new DescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out set);

    _textureSets.SetAt(set, index);
  }

  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<SpriteUniformBufferObject>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    // pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
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
    if (_configInfo == null) {
      _configInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _configInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "sprite_vertex", "sprite_fragment", pipelineConfig, new PipelineSpriteProvider());
  }

  public override IRenderSystem Create(Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSet,
    PipelineConfigInfo configInfo = null!
  ) {
    return new Render2DSystem(device, renderer, globalSet, configInfo);
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    for (int i = 0; i < _spriteBuffer.Length; i++) {
      _spriteBuffer[i]?.Dispose();
    }
    _pool?.FreeDescriptors(_descriptorSets);
    _pool?.Dispose();
    _texturePool?.FreeDescriptors(_textureSets);
    _texturePool?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}
