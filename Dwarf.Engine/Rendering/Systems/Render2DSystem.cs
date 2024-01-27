using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;
public class Render2DSystem : SystemBase, IRenderSystem {
  private PublicList<VkDescriptorSet> _textureSets = new();
  private Vulkan.Buffer _spriteBuffer = null!;

  public Render2DSystem(
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

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
      _setLayout.GetDescriptorSetLayout(),
      _textureSetLayout.GetDescriptorSetLayout()
    ];
    CreatePipelineLayout<SpriteUniformBufferObject>(descriptorSetLayouts);
    CreatePipeline(renderer.GetSwapchainRenderPass(), "sprite_vertex", "sprite_fragment", new PipelineSpriteProvider());
  }

  public unsafe void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 2D renderer are less than 1, thus 2D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 2D");

    _descriptorPool = new DescriptorPool.Builder(_device)
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

    _spriteBuffer = new Vulkan.Buffer(
        _device,
        (ulong)Unsafe.SizeOf<SpriteUniformBufferObject>(),
        (uint)entities.Length,
        VkBufferUsageFlags.UniformBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );
    _descriptorSets = new VkDescriptorSet[entities.Length];
    _textureSets = new();

    for (int x = 0; x < entities.Length; x++) {
      _textureSets.Add(new());
    }

    for (int i = 0; i < entities.Length; i++) {
      var targetSprite = entities[i].GetComponent<Sprite>();
      if (targetSprite.UsesTexture) {
        BindDescriptorTexture(targetSprite.Owner!, ref textures, i);
      }

      var bufferInfo = _spriteBuffer.GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<SpriteUniformBufferObject>());
      _ = new DescriptorWriter(_setLayout, _descriptorPool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
    }
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (entities.Length > (uint)_spriteBuffer.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (uint)_spriteBuffer.GetInstanceCount()) {
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

  public unsafe void Render(FrameInfo frameInfo, Span<Entity> entities) {
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

      var pushConstantData = new SpriteUniformBufferObject {
        SpriteMatrix = entities[i].GetComponent<Transform>().Matrix4,
        SpriteColor = entities[i].GetComponent<Material>().GetColor(),
        UseTexture = true
      };

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<SpriteUniformBufferObject>(),
        &pushConstantData
      );

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
    VkDescriptorImageInfo imageInfo = new() {
      sampler = texture.GetSampler(),
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = texture.GetImageView()
    };
    VkDescriptorSet set;
    _ = new DescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out set);

    _textureSets.SetAt(set, index);
  }

  public override unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _spriteBuffer?.Dispose();
    _descriptorPool?.FreeDescriptors(_descriptorSets);
    _texturePool?.FreeDescriptors(_textureSets);

    base.Dispose();
  }
}
