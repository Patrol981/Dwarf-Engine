using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering.UI;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public class RenderUISystem : SystemBase {
  private PublicList<VkDescriptorSet> _textureSets = new PublicList<VkDescriptorSet>();
  private DwarfBuffer _uiBuffer = null!;

  public RenderUISystem(
    VulkanDevice device,
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
      _textureSetLayout.GetDescriptorSetLayout(),
    ];

    CreatePipelineLayout<UIUniformObject>(descriptorSetLayouts);
    CreatePipeline(_renderer.GetSwapchainRenderPass(), "gui_vertex", "gui_fragment", new PipelineUIProvider());
  }

  public unsafe void Setup(Canvas canvas, ref TextureManager textureManager) {
    var entities = canvas.GetUI();

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using UI Rendering are less than 1, thus UI Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating UI Renderer");

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

    _uiBuffer = new DwarfBuffer(
        _device,
        (ulong)Unsafe.SizeOf<UIUniformObject>(),
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
      var targetUI = entities[i].GetDrawable<IUIElement>();
      BindDescriptorTexture(targetUI.Owner!, ref textureManager, i);

      var bufferInfo = _uiBuffer.GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<UIUniformObject>());
      _ = new VulkanDescriptorWriter(_setLayout, _descriptorPool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
    }
  }

  public unsafe void DrawUI(FrameInfo frameInfo, Canvas canvas) {
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

    var entities = canvas.GetUI();

    for (int i = 0; i < entities.Length; i++) {
      var uiPushConstant = new UIUniformObject {
        UIMatrix = entities[i].GetComponent<RectTransform>().Matrix4
      };

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        _pipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<UIUniformObject>(),
        &uiPushConstant
      );

      var uiComponent = entities[i].GetDrawable<IUIElement>() as IUIElement;
      uiComponent?.Update();
      Descriptor.BindDescriptorSet((VulkanDevice)_device, _textureSets.GetAt(i), frameInfo, ref _pipelineLayout, 2, 1);
      uiComponent?.Bind(frameInfo.CommandBuffer, 0);
      uiComponent?.Draw(frameInfo.CommandBuffer, 0);
    }
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (entities.Length > (uint)_uiBuffer.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (uint)_uiBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = entities.Length;
    var sets = _textureSets.Size;
    return len == sets;
  }

  private unsafe void BindDescriptorTexture(Entity entity, ref TextureManager textureManager, int index) {
    var id = entity.GetDrawable<IUIElement>() as IUIElement;
    var texture = textureManager.GetTexture(id!.GetTextureIdReference());

    VkDescriptorImageInfo imageInfo = new() {
      sampler = texture.GetSampler(),
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = texture.GetImageView()
    };
    _ = new VulkanDescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out VkDescriptorSet set);
    _textureSets.SetAt(set, index);
  }

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    _pipelineConfigInfo ??= new UIPipeline();
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "gui_vertex", "gui_fragment", pipelineConfig, new PipelineUIProvider());
  }

  private unsafe void CreatePipelineLayout_Old(VkDescriptorSetLayout[] layouts) {
    VkPushConstantRange pushConstantRange = new() {
      stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      offset = 0,
      size = (uint)Unsafe.SizeOf<UIUniformObject>()
    };

    VkPipelineLayoutCreateInfo pipelineInfo = new() {
      setLayoutCount = (uint)layouts.Length
    };
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _uiBuffer?.Dispose();
    _descriptorPool?.FreeDescriptors(_descriptorSets);
    _texturePool?.FreeDescriptors(_textureSets);

    base.Dispose();
  }
}