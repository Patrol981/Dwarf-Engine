using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using static Dwarf.Extensions.GLFW.GLFWKeyMap;

using ImGuiNET;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;
using DwarfEngine.Engine;
using Assimp;
using DwarfEngine.Engine.Rendering.UI;

namespace Dwarf.Engine.Rendering;

public class RenderUISystem : SystemBase, IRenderSystem {
  private readonly Device _device = null!;
  private readonly Renderer _renderer = null!;
  private PipelineConfigInfo _configInfo = null!;
  private Pipeline _pipeline = null!;
  private VkPipelineLayout _pipelineLayout;

  private Vulkan.Buffer[] _uiBuffer = new Vulkan.Buffer[0];
  private DescriptorPool _uiPool = null!;
  private DescriptorPool _uiTexturePool = null!;
  private DescriptorSetLayout _uiSetLayout = null!;
  private DescriptorSetLayout _uiTextureSetLayout = null!;
  private VkDescriptorSet[] _uiDescriptorSets = new VkDescriptorSet[0];
  private PublicList<VkDescriptorSet> _uiTextureDescriptorSets = new();

  private int _texturesCount = 0;

  public RenderUISystem() { }

  public RenderUISystem(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) {
    _device = device;
    _renderer = renderer;
    _configInfo = configInfo;

    _uiSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    _uiTextureSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
      .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = new VkDescriptorSetLayout[] {
      globalSetLayout,
      _uiSetLayout.GetDescriptorSetLayout(),
      _uiTextureSetLayout.GetDescriptorSetLayout(),
    };

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(_renderer.GetSwapchainRenderPass());
  }

  public override IRenderSystem Create(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSet,
    PipelineConfigInfo configInfo = null!
  ) {
    return new RenderUISystem(device, renderer, globalSet, configInfo);
  }

  public unsafe void SetupUIData(ReadOnlySpan<Entity> entities, ref TextureManager textureManager) {
    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using UI Rendering are less than 1, thus UI Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating UI Renderer");

    _uiPool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)entities.Length)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)entities.Length)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = entities.Length;

    _uiTexturePool = new DescriptorPool.Builder(_device)
    .SetMaxSets((uint)_texturesCount)
    .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
    .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
    .Build();

    _uiBuffer = new Vulkan.Buffer[entities.Length];
    _uiDescriptorSets = new VkDescriptorSet[entities.Length];
    _uiTextureDescriptorSets = new();

    for (int x = 0; x < entities.Length; x++) {
      _uiTextureDescriptorSets.Add(new());
    }

    for (int i = 0; i < entities.Length; i++) {
      _uiBuffer[i] = new Vulkan.Buffer(
        _device,
        (ulong)Unsafe.SizeOf<SpriteUniformBufferObject>(),
        1,
        VkBufferUsageFlags.UniformBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );

      var targetUI = entities[i].GetComponent<TextField>();
      BindDescriptorTexture(targetUI.Owner!, ref textureManager, i);

      var bufferInfo = _uiBuffer[i].GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<UIUniformObject>());
      var writer = new DescriptorWriter(_uiSetLayout, _uiPool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _uiDescriptorSets[i]);
    }
  }

  public unsafe void DrawUI(FrameInfo frameInfo, Span<Entity> entities) {
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
      var uiUBO = new UIUniformObject();
      uiUBO.UIMatrix = entities[i].GetComponent<Transform>().Matrix4;
      uiUBO.UIColor = new Vector3(1, 1, 1);

      _uiBuffer[i].Map((ulong)Unsafe.SizeOf<UIUniformObject>());
      _uiBuffer[i].WriteToBuffer((IntPtr)(&uiUBO), (ulong)Unsafe.SizeOf<UIUniformObject>());
      _uiBuffer[i].Unmap();

      fixed (VkDescriptorSet* ptr = &_uiDescriptorSets[i]) {
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

      var uiComponent = entities[i].GetComponent<TextField>();
      uiComponent.RenderText();
      uiComponent.BindDescriptorSet(_uiTextureDescriptorSets.GetAt(i), frameInfo, ref _pipelineLayout);
      uiComponent.Bind(frameInfo.CommandBuffer);
      uiComponent.Draw(frameInfo.CommandBuffer);
    }
  }

  private unsafe void BindDescriptorTexture(Entity entity, ref TextureManager textureManager, int index) {
    var id = entity.GetComponent<TextField>().GetTextureIdReference();
    var texture = textureManager.GetTexture(id);
    VkDescriptorImageInfo imageInfo = new();
    imageInfo.sampler = texture.GetSampler();
    imageInfo.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
    imageInfo.imageView = texture.GetImageView();
    VkDescriptorSet set;
    var texWriter = new DescriptorWriter(_uiTextureSetLayout, _uiTexturePool)
      .WriteImage(0, &imageInfo)
      .Build(out set);
    _uiTextureDescriptorSets.SetAt(set, index);
  }

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    if (_configInfo == null) {
      _configInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _configInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "gui_vertex", "gui_fragment", pipelineConfig, new PipelineUIProvider());
  }

  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
    pipelineInfo.setLayoutCount = (uint)layouts.Length;
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
    pipelineInfo.pushConstantRangeCount = 0;
    pipelineInfo.pPushConstantRanges = null;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _uiTextureSetLayout?.Dispose();
    _uiSetLayout?.Dispose();

    for (int i = 0; i < _uiBuffer.Length; i++) {
      _uiBuffer[i]?.Dispose();
    }

    _uiPool?.FreeDescriptors(_uiDescriptorSets);
    _uiPool?.Dispose();

    _uiTexturePool?.FreeDescriptors(_uiTextureDescriptorSets);
    _uiTexturePool?.Dispose();

    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}