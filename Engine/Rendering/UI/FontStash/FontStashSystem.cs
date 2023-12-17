using Dwarf.Vulkan;
using static Vortice.Vulkan.Vulkan;

using FontStashSharp.Interfaces;

using Vortice.Vulkan;
using Dwarf.Engine.Physics;
using System.Runtime.CompilerServices;
using System.Numerics;
using Dwarf.Engine.EntityComponentSystem;
using FontStashSharp;

namespace Dwarf.Engine.Rendering.UI.FontStash;
public class FontStashSystem : SystemBase, IFontStashRenderer2, IRenderSystem {
  private const int MAX_SPRITES = 2048;
  private const int MAX_VERTICES = MAX_SPRITES * 4;
  private const int MAX_INDICES = MAX_SPRITES * 6;

  private readonly FontStashManager _textureManager;
  public ITexture2DManager TextureManager => _textureManager;

  private Matrix4x4 _transform;

  private FontSystem _fontSystem;
  private FontSystemSettings _fontSystemSettings;
  private FontStashObject _fontStashObject;

  private int _vertexIndex = 0;
  private object _lastTexture = null!;

  public FontStashSystem(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    _textureManager = new FontStashManager();

    CreatePipelineLayout();
    CreatePipeline(renderer.GetSwapchainRenderPass());

    var windowSize = Application.Instance.Window.Extent;
    _transform = Matrix4x4.CreateOrthographicOffCenter(0, windowSize.width, windowSize.height, 0, 0, -1);

    _fontSystemSettings = new FontSystemSettings {
      FontResolutionFactor = 2,
      KernelWidth = 2,
      KernelHeight = 2
    };

    _fontSystem = new FontSystem();
    var stream = File.ReadAllBytes("./Fonts/DroidSans.ttf");
    _fontSystem.AddFont(stream);

    _fontStashObject = new(device);
  }

  public unsafe void Render(FrameInfo frameInfo) {
    _pipeline.Bind(frameInfo.CommandBuffer);


  }

  private unsafe void CreatePipelineLayout() {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<ColliderMeshPushConstant>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.setLayoutCount = 0;
    pipelineInfo.pSetLayouts = null;
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private unsafe void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    if (_pipelineConfigInfo == null) {
      _pipelineConfigInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(
      _device,
      "font_stash_vertex",
      "font_stash_fragment",
      pipelineConfig,
      new PipelineFontStashProvider()
    );
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }

  public void DrawQuad(object texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight, ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight) {
    throw new NotImplementedException();
  }

  private void FlushBuffer() {
    if (_vertexIndex == 0 || _lastTexture == null) {
      return;
    }

    _fontStashObject.SetVertexData(0, _vertexIndex);
  }
}
