using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Renderer2D.Interfaces;
using Dwarf.Rendering.Renderer2D.Models;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Renderer2D;

public class Render2DSystem : SystemBase {
  private DwarfBuffer _spriteBuffer = null!;
  // private unsafe SpritePushConstant* _spritePushConstant;

  private int _prevTexCount = -1;
  private const uint MAX_SETS = 10000;
  // private int _texCount = -1;

  public Render2DSystem(
    VmaAllocator vmaAllocator,
    VulkanDevice device,
    IRenderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.SampledImage, ShaderStageFlags.Fragment)
      .AddBinding(1, DescriptorType.Sampler, ShaderStageFlags.Fragment)
      .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
      _setLayout.GetDescriptorSetLayout(),
      _textureSetLayout.GetDescriptorSetLayout()
    ];

    AddPipelineData<SpritePushConstant>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "sprite_vertex",
      FragmentName = "sprite_fragment",
      PipelineProvider = new PipelineSpriteProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(MAX_SETS)
      .AddPoolSize(DescriptorType.SampledImage, (uint)_texturesCount)
      .AddPoolSize(DescriptorType.Sampler, (uint)_texturesCount)
      .SetPoolFlags(DescriptorPoolCreateFlags.None)
      .Build();

    _texturesCount = -1;
  }

  public unsafe void Setup(ReadOnlySpan<IDrawable2D> drawables, ref TextureManager textures) {
    if (drawables.Length < 1) {
      Logger.Warn("Entities that are capable of using 2D renderer are less than 1, thus 2D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 2D");

    // _spritePushConstant =
    //   (SpritePushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<SpritePushConstant>());

    _texturesCount = CalculateTextureCount(drawables);

    if (_texturesCount > MAX_SETS) {
      _descriptorPool?.Dispose();
      _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets((uint)_texturesCount)
      .AddPoolSize(DescriptorType.SampledImage, (uint)_texturesCount)
      .AddPoolSize(DescriptorType.Sampler, (uint)_texturesCount)
      .SetPoolFlags(DescriptorPoolCreateFlags.None)
      .Build();
    }

    // _spriteBuffer?.Dispose();
    // _spriteBuffer = new DwarfBuffer(
    //   _vmaAllocator,
    //   _device,
    //   (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>(),
    //   (uint)_texturesCount,
    //   BufferUsage.IndirectBuffer,
    //   MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
    //   ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
    // );


    for (int i = 0; i < drawables.Length; i++) {
      if (drawables[i].DescriptorBuilt) continue;
      drawables[i].BuildDescriptors(_textureSetLayout, _descriptorPool);
    }
  }

  public bool CheckSizes(ReadOnlySpan<IDrawable2D> drawables) {
    // if (_texturesCount == -1) {
    //   var textureManager = Application.Instance.TextureManager;
    //   // Setup(drawables, ref textureManager);
    // }
    var newCount = CalculateTextureCount(drawables);
    if (newCount > _texturesCount) {
      return false;
    }

    return true;
  }

  public bool CheckSizesOld(ReadOnlySpan<IDrawable2D> drawables) {
    if (_spriteBuffer == null) {
      var textureManager = Application.Instance.TextureManager;
      Setup(drawables, ref textureManager);
    }
    var texCount = CalculateTextureCount(drawables);
    if (texCount > (uint)_spriteBuffer!.GetInstanceCount()) {
      return false;
    } else if (texCount < (uint)_spriteBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  private static int CalculateTextureCount(ReadOnlySpan<IDrawable2D> drawables) {
    int count = 0;
    for (int i = 0; i < drawables.Length; i++) {
      count += drawables[i].SpriteCount;
    }
    return count;
  }

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

    string lastTexture = "";
    uint lastVertCount = 0;

    for (int i = 0; i < drawables.Length; i++) {
      if (!drawables[i].Active || drawables[i].Entity.CanBeDisposed) continue;

      var pushConstantData = new SpritePushConstant {
        SpriteMatrix = drawables[i].Entity.GetComponent<Transform>().Matrix4,
        SpriteSheetData = new(drawables[i].SpriteSheetSize.X, drawables[i].SpriteSheetSize.Y, drawables[i].SpriteIndex),
        FlipX = drawables[i].FlipX,
        FlipY = drawables[i].FlipY
      };

      vkCmdPushConstants(
          frameInfo.CommandBuffer,
          PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<SpritePushConstant>(),
          &pushConstantData
        );

      if (!drawables[i].Entity.CanBeDisposed && drawables[i].Active) {
        if (lastTexture != drawables[i].Texture?.TextureName) {
          var pipelineLayout = _pipelines["main"].PipelineLayout;
          if (drawables[i].NeedPipelineCache) {
            drawables[i].CachePipelineLayout(pipelineLayout);
          } else {
            Descriptor.BindDescriptorSet(drawables[i].Texture.TextureDescriptor, frameInfo, pipelineLayout, 2, 1);
          }
          lastTexture = drawables[i].Texture?.TextureName ?? "";
        }

        if (lastVertCount != drawables[i].CollisionMesh.VertexCount) {
          drawables[i].Bind(frameInfo.CommandBuffer, 0);
          lastVertCount = (uint)drawables[i].CollisionMesh.VertexCount;
        }

        drawables[i].Draw(frameInfo.CommandBuffer);
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _spriteBuffer?.Dispose();
    // MemoryUtils.FreeIntPtr<SpritePushConstant>((nint)_spritePushConstant);
    // Marshal.FreeHGlobal((nint)_spritePushConstant);
    base.Dispose();
  }
}
