using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Renderer2D;
public class Render2DSystem : SystemBase {
  private DwarfBuffer _spriteBuffer = null!;

  public Render2DSystem(
    VmaAllocator vmaAllocator,
    VulkanDevice device,
    IRenderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.SampledImage, VkShaderStageFlags.Fragment)
      .AddBinding(1, VkDescriptorType.Sampler, VkShaderStageFlags.Fragment)
      .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
      _setLayout.GetDescriptorSetLayout(),
      _textureSetLayout.GetDescriptorSetLayout()
    ];

    AddPipelineData<SpriteUniformBufferObject>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "sprite_vertex",
      FragmentName = "sprite_fragment",
      PipelineProvider = new PipelineSpriteProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public unsafe void Setup(ReadOnlySpan<IDrawable2D> drawables, ref TextureManager textures) {
    if (drawables.Length < 1) {
      Logger.Warn("Entities that are capable of using 2D renderer are less than 1, thus 2D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 2D");

    _texturesCount = drawables.Length;

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(5)
      .AddPoolSize(VkDescriptorType.SampledImage, (uint)_texturesCount)
      .AddPoolSize(VkDescriptorType.Sampler, (uint)_texturesCount)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.None)
      .Build();


    _spriteBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      (ulong)Unsafe.SizeOf<SpriteUniformBufferObject>(),
      (uint)drawables.Length,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
    );


    for (int i = 0; i < drawables.Length; i++) {
      drawables[i].BuildDescriptors(_textureSetLayout, _descriptorPool);
    }
  }

  public bool CheckSizes(ReadOnlySpan<IDrawable2D> drawables) {
    if (_spriteBuffer == null) {
      var textureManager = Application.Instance.TextureManager;
      Setup(drawables, ref textureManager);
    }
    if (drawables.Length > (uint)_spriteBuffer!.GetInstanceCount()) {
      return false;
    } else if (drawables.Length < (uint)_spriteBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  /*
  public unsafe void Setup_Old(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 2D renderer are less than 1, thus 2D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 2D");

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets((uint)entities.Length)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)entities.Length)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = entities.Length;

    _texturePool = new DescriptorPool.Builder((VulkanDevice)_device)
    .SetMaxSets((uint)_texturesCount)
    .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
    .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
    .Build();

    _spriteBuffer = new DwarfBuffer(
      _vmaAllocator,
      _device,
      (ulong)Unsafe.SizeOf<SpriteUniformBufferObject>(),
      (uint)entities.Length,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
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
      _ = new VulkanDescriptorWriter(_setLayout, _descriptorPool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
    }
  }



  public bool CheckTextures(ReadOnlySpan<IDrawable2D> drawables) {
    var len = drawables.Length;
    var sets = _textureSets.Size;
    return len == sets;
  }

  public bool CheckSizes_Old(ReadOnlySpan<Entity> entities) {
    if (_spriteBuffer == null) {
      var textureManager = Application.Instance.TextureManager;
      Setup_Old(entities, ref textureManager);
    }
    if (entities.Length > (uint)_spriteBuffer!.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (uint)_spriteBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }


  public bool CheckTextures_Old(ReadOnlySpan<Entity> entities) {
    var len = entities.Length;
    var sets = _textureSets.Size;
    return len == sets;
  }
  */

  public unsafe void Render(FrameInfo frameInfo, ReadOnlySpan<IDrawable2D> drawables) {
    BindPipeline(frameInfo.CommandBuffer);

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      0,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    for (int i = 0; i < drawables.Length; i++) {
      if (!drawables[i].Active) continue;

      var pushConstantData = new SpriteUniformBufferObject {
        SpriteMatrix = drawables[i].Entity.GetComponent<Transform>().Matrix4,
        SpriteColor = drawables[i].Entity.GetComponent<MaterialComponent>().Color,
        UseTexture = true
      };

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<SpriteUniformBufferObject>(),
        &pushConstantData
      );

      // var sprite = drawables[i].Entity.GetComponent<Sprite>();
      if (!drawables[i].Entity.CanBeDisposed && drawables[i].Active) {
        // if (sprite.UsesTexture)
        // Descriptor.BindDescriptorSet(_textureSets.GetAt(i), frameInfo, _pipelines["main"].PipelineLayout, 2, 1);
        Descriptor.BindDescriptorSet(drawables[i].Texture.TextureDescriptor, frameInfo, _pipelines["main"].PipelineLayout, 2, 1);
        // sprite.BindDescriptorSet(_textureSets.GetAt(i), frameInfo, _pipelines["main"].PipelineLayout);
        drawables[i].Bind(frameInfo.CommandBuffer, 0);
        drawables[i].Draw(frameInfo.CommandBuffer);
      }
    }
  }

  private unsafe void BindDescriptorTexture(Entity entity, ref TextureManager textureManager, int index) {
    var id = entity.GetComponent<Sprite>().GetTextureIdReference();
    var texture = textureManager.GetTextureLocal(id);
    VkDescriptorImageInfo imageInfo = new() {
      sampler = texture.Sampler,
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = texture.ImageView
    };
    VkDescriptorSet set;
    _ = new VulkanDescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out set);

    // _textureSets.SetAt(set, index);
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _spriteBuffer?.Dispose();
    base.Dispose();
  }
}
