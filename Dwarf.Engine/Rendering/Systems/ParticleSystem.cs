using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;
using Dwarf.Rendering.Particles;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Dwarf.Utils;
using System.Numerics;

namespace Dwarf.Rendering;

public class ParticleSystem : SystemBase {
  private static List<Particle> s_particles = [];
  private readonly unsafe ParticlePushConstant* _particlePushConstant =
    (ParticlePushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<ParticlePushConstant>());

  public ParticleSystem(
    VmaAllocator vmaAllocator,
    IDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData<ParticlePushConstant>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "particle_vertex",
      FragmentName = "particle_fragment",
      PipelineProvider = new ParticlePipelineProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public void Setup(ref TextureManager textures) {
    if (s_particles.Count < 1) {
      Logger.Warn("Particles that are capable of using particle renderer are less than 1, thus Particle Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Particle System");

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets((uint)s_particles.Count)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)s_particles.Count)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = s_particles.Count;

    _texturePool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets((uint)_texturesCount)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();
  }

  public void Update() {
    for (int i = 0; i < s_particles.Count; i++) {
      if (!s_particles[i].Update()) {
        s_particles[i].MarkToDispose();
      }
    }
  }

  public void Render(FrameInfo frameInfo) {
    if (s_particles.Count < 1) return;

    BindPipeline(frameInfo.CommandBuffer);
    unsafe {
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
    }

    for (int i = 0; i < s_particles.Count; i++) {
      unsafe {
        _particlePushConstant->Color = Vector4.One;
        _particlePushConstant->Position = new Vector4(s_particles[i].Position, 1.0f);
        _particlePushConstant->Radius = s_particles[i].Scale;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<ParticlePushConstant>(),
          _particlePushConstant
        );

        vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
      }
    }
  }

  public static void AddParticle(Particle particle) {
    s_particles.Add(particle);
  }

  public static void AddParticles(ParticleBatch batch) {
    s_particles.AddRange(batch.Particles);
  }

  public bool ValidateTextures() {
    return _texturesCount == s_particles.Count;
  }

  public void Collect() {
    for (int i = 0; i < s_particles.Count; i++) {
      if (s_particles[i].CanBeDisposed) {
        s_particles.RemoveAt(i);
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    MemoryUtils.FreeIntPtr<ParticlePushConstant>((nint)_particlePushConstant);

    base.Dispose();
  }
}