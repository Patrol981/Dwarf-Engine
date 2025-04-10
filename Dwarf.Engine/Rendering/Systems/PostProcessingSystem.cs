using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct PostProcessInfo {
  public float Float_1_1;
  public float Float_1_2;
  public float Float_1_3;
  public float Float_1_4;

  public float Float_2_1;
  public float Float_2_2;
  public float Float_2_3;
  public float Float_2_4;

  public float Float_3_1;
  public float Float_3_2;
  public float Float_3_3;
  public float Float_3_4;

  public float Float_4_1;
  public float Float_4_2;
  public float Float_4_3;
  public float Float_4_4;

  public float Float_5_1;
  public float Float_5_2;
  public float Float_5_3;
  public float Float_5_4;

  public float Float_6_1;
  public float Float_6_2;
  public float Float_6_3;
  public float Float_6_4;

  public float Float_7_1;
  public float Float_7_2;
  public float Float_7_3;
  public float Float_7_4;

  public float Float_8_1;
  public float Float_8_2;
  public float Float_8_3;
  public float Float_8_4;
}

public class PostProcessingSystem : SystemBase {
  // public static float DepthMax = 0.995f;
  // public static float DepthMin = 0.990f;
  // public static float EdgeLow = 100f;
  // public static float EdgeHigh = 65f;
  // public static float Contrast = 0.5f; // 2.0f / 0.56f
  // public static float Stipple = 64f; // 0.39f / 0.706f
  // public static Vector3 Luminance = new(0.299f, 0.587f, 0.114f);

  public static PostProcessInfo PostProcessInfo = new() {
    Float_1_1 = 1.4f,
    Float_1_2 = 65,
    Float_1_3 = 0.5f,
    Float_1_4 = 128,

    Float_2_1 = 0.299f,
    Float_2_2 = 0.587f,
    Float_2_3 = 0.114f,
    Float_2_4 = 1,

    Float_3_1 = 0.9f,
    Float_3_4 = 1.0f,

    Float_4_1 = 0.3f,
    Float_4_2 = 0.7f,
    Float_4_3 = 0.5f,
    Float_4_4 = 0.3f,

    Float_5_1 = 0.2f,
    Float_5_2 = 0.0f,
    Float_5_3 = 0.4f,
    Float_5_4 = 0.4f,

    Float_6_1 = 0.5f,
    Float_6_2 = 0.0f,
    Float_6_3 = 0.0f,
    Float_6_4 = 0.2f,

    Float_7_1 = 0.1f,
    Float_7_2 = 0.0f,
  };

  private readonly unsafe PostProcessInfo* _postProcessInfoPushConstant =
    (PostProcessInfo*)Marshal.AllocHGlobal(Unsafe.SizeOf<PostProcessInfo>());
  private readonly TextureManager _textureManager;

  // private VulkanTexture _inputTexture1 = null!;
  // private VulkanTexture _inputTexture2 = null!;
  // private VulkanTexture _inputTexture3 = null!;

  // private const string HatchOneTextureName = "./Resources/zaarg.png";
  // // "./Resources/twilight-5-1x.png";
  // // "./Resources/lv-corinthian-slate-801-1x.png";
  // // "./Resources/zaarg.png";
  // private const string HatchTwoTextureName = "./Resources/slso8-1x.png";
  // private const string HatchThreeTextureName = "./Resources/justparchment8-1x.png";

  private readonly VulkanTexture[] _inputTextures = [];

  public PostProcessingSystem(
    VmaAllocator vmaAllocator,
    IDevice device,
    IRenderer renderer,
    SystemConfiguration systemConfiguration,
    Dictionary<string, DescriptorSetLayout> externalLayouts,
    PipelineConfigInfo configInfo = null!
  ) : base(vmaAllocator, device, renderer, configInfo) {
    _textureManager = Application.Instance.TextureManager;

    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.AllGraphics)
      .AddBinding(1, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.AllGraphics)
      // .AddBinding(2, VkDescriptorType.SampledImage, VkShaderStageFlags.AllGraphics)
      // .AddBinding(3, VkDescriptorType.Sampler, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.SampledImage, VkShaderStageFlags.AllGraphics)
      .AddBinding(1, VkDescriptorType.Sampler, VkShaderStageFlags.AllGraphics)
      .Build();

    VkDescriptorSetLayout[] layouts = [
      // renderer.Swapchain.InputAttachmentLayout.GetDescriptorSetLayout(),
      // externalLayouts["Global"].GetDescriptorSetLayout()
      _setLayout.GetDescriptorSetLayout(),
      externalLayouts["Global"].GetDescriptorSetLayout(),
      _textureSetLayout.GetDescriptorSetLayout(),
      _textureSetLayout.GetDescriptorSetLayout(),
      _textureSetLayout.GetDescriptorSetLayout()
    ];

    AddPipelineData<PostProcessInfo>(new() {
      RenderPass = renderer.GetPostProcessingPass(),
      VertexName = "post_process_index_vertex",
      FragmentName = "post_process_jpaint0_fragment",
      // FragmentName = "post_process_toon_shader_fragment",
      // FragmentName = "post_process_kuwahara_shader_fragment",
      // FragmentName = "post_process_waterpaint_fragment",
      PipelineProvider = new SecondSubpassPipelineProvider(),
      DescriptorSetLayouts = layouts
    });

    if (systemConfiguration.PostProcessInputTextures != null) {
      _inputTextures = new VulkanTexture[systemConfiguration.PostProcessInputTextures.Length];
    }

    Setup(systemConfiguration);
  }

  public void Setup(SystemConfiguration systemConfiguration) {
    _device.WaitQueue();

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(4)
      .AddPoolSize(VkDescriptorType.SampledImage, 10)
      .AddPoolSize(VkDescriptorType.Sampler, 10)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 20)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    var texLen = systemConfiguration.PostProcessInputTextures?.Length;
    if (!texLen.HasValue) return;

    for (int i = 0; i < texLen; i++) {
      if (_inputTextures[i] != null) continue;
      _textureManager.AddTextureGlobal(systemConfiguration.PostProcessInputTextures![i]).Wait();
      var id = _textureManager.GetTextureIdGlobal(systemConfiguration.PostProcessInputTextures[i]);
      _inputTextures[i] = (VulkanTexture)_textureManager.GetTextureGlobal(id);
      _inputTextures[i].BuildDescriptor(_textureSetLayout, _descriptorPool);
    }
  }

  private void UpdateDescriptors(int currentFrame) {
    // _renderer.Swapchain.UpdateDescriptors(currentFrame);
    // _ renderer.Swapchain.UpdatePostProcessDescriptors(currentFrame);
    _renderer.UpdateDescriptors();
  }

  public void Render(FrameInfo frameInfo) {
    UpdateDescriptors(_renderer.FrameIndex);
    BindPipeline(frameInfo.CommandBuffer);

    var window = Application.Instance.Window;
    unsafe {
      _postProcessInfoPushConstant->Float_1_1 = PostProcessInfo.Float_1_1;
      _postProcessInfoPushConstant->Float_1_2 = PostProcessInfo.Float_1_2;
      _postProcessInfoPushConstant->Float_1_3 = PostProcessInfo.Float_1_3;
      _postProcessInfoPushConstant->Float_1_4 = PostProcessInfo.Float_1_4;

      _postProcessInfoPushConstant->Float_2_1 = PostProcessInfo.Float_2_1;
      _postProcessInfoPushConstant->Float_2_2 = PostProcessInfo.Float_2_2;
      _postProcessInfoPushConstant->Float_2_3 = PostProcessInfo.Float_2_3;
      _postProcessInfoPushConstant->Float_2_4 = PostProcessInfo.Float_2_4;

      _postProcessInfoPushConstant->Float_3_1 = PostProcessInfo.Float_3_1;
      _postProcessInfoPushConstant->Float_3_2 = PostProcessInfo.Float_3_2;
      _postProcessInfoPushConstant->Float_3_3 = PostProcessInfo.Float_3_3;
      _postProcessInfoPushConstant->Float_3_4 = PostProcessInfo.Float_3_4;

      _postProcessInfoPushConstant->Float_4_1 = PostProcessInfo.Float_4_1;
      _postProcessInfoPushConstant->Float_4_2 = PostProcessInfo.Float_4_2;
      _postProcessInfoPushConstant->Float_4_3 = PostProcessInfo.Float_4_3;
      _postProcessInfoPushConstant->Float_4_4 = PostProcessInfo.Float_4_4;

      _postProcessInfoPushConstant->Float_5_1 = PostProcessInfo.Float_5_1;
      _postProcessInfoPushConstant->Float_5_2 = PostProcessInfo.Float_5_2;
      _postProcessInfoPushConstant->Float_5_3 = PostProcessInfo.Float_5_3;
      _postProcessInfoPushConstant->Float_5_4 = PostProcessInfo.Float_5_4;

      _postProcessInfoPushConstant->Float_6_1 = PostProcessInfo.Float_6_1;
      _postProcessInfoPushConstant->Float_6_2 = PostProcessInfo.Float_6_2;
      _postProcessInfoPushConstant->Float_6_3 = PostProcessInfo.Float_6_3;
      _postProcessInfoPushConstant->Float_6_4 = PostProcessInfo.Float_6_4;

      _postProcessInfoPushConstant->Float_7_1 = PostProcessInfo.Float_7_1;
      _postProcessInfoPushConstant->Float_7_2 = PostProcessInfo.Float_7_2;
      _postProcessInfoPushConstant->Float_7_3 = PostProcessInfo.Float_7_3;
      _postProcessInfoPushConstant->Float_7_4 = PostProcessInfo.Float_7_4;

      _postProcessInfoPushConstant->Float_8_1 = PostProcessInfo.Float_8_1;
      _postProcessInfoPushConstant->Float_8_2 = PostProcessInfo.Float_8_2;
      _postProcessInfoPushConstant->Float_8_3 = PostProcessInfo.Float_8_3;
      _postProcessInfoPushConstant->Float_8_4 = PostProcessInfo.Float_8_4;

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<PostProcessInfo>(),
        _postProcessInfoPushConstant
      );
    }

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      0,
      _renderer.PostProcessDecriptor
    );

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      1,
      frameInfo.GlobalDescriptorSet
    );

    for (uint i = 2, j = 0; i <= _inputTextures.Length + 1; i++, j++) {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        PipelineLayout,
        i,
        _inputTextures[j].TextureDescriptor
      );
    }

    vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
  }

  public unsafe override void Dispose() {
    MemoryUtils.FreeIntPtr<PostProcessInfo>((nint)_postProcessInfoPushConstant);
    base.Dispose();
  }
}