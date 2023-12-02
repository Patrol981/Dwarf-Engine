using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using ImGuiNET;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;
using Dwarf.Engine;
using Assimp;
using Dwarf.Engine.Rendering.UI;

namespace Dwarf.Engine.Rendering;

public class RenderUISystem : SystemBase, IRenderSystem {
  private PublicList<VkDescriptorSet> _textureSets = new PublicList<VkDescriptorSet>();
  private Vulkan.Buffer _uiBuffer = null!;

  public RenderUISystem(
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
      _textureSetLayout.GetDescriptorSetLayout(),
    };

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(_renderer.GetSwapchainRenderPass());
  }

  public unsafe void SetupUIData(Canvas canvas, ref TextureManager textureManager) {
    var entities = canvas.GetUI();

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using UI Rendering are less than 1, thus UI Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating UI Renderer");

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

    _uiBuffer = new Vulkan.Buffer(
        _device,
        (ulong)Unsafe.SizeOf<UIUniformObject>(),
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
      // var targetUI = entities[i].GetComponent<TextField>();
      var targetUI = entities[i].GetDrawable<IUIElement>();
      BindDescriptorTexture(targetUI.Owner!, ref textureManager, i);

      var bufferInfo = _uiBuffer.GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<UIUniformObject>());
      var writer = new DescriptorWriter(_setLayout, _descriptorPool)
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
      var uiPushConstant = new UIUniformObject();
      uiPushConstant.UIMatrix = entities[i].GetComponent<RectTransform>().Matrix4;

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
      uiComponent?.BindDescriptorSet(_textureSets.GetAt(i), frameInfo, ref _pipelineLayout);
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
    if (len != sets) {
      return false;
    }

    return true;
  }

  private unsafe void BindDescriptorTexture(Entity entity, ref TextureManager textureManager, int index) {
    // var id = entity.GetComponent<TextField>().GetTextureIdReference();
    var id = entity.GetDrawable<IUIElement>() as IUIElement;

    var texture = textureManager.GetTexture(id!.GetTextureIdReference());
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

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    if (_pipelineConfigInfo == null) {
      _pipelineConfigInfo = new UIPipeline();
    }
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "gui_vertex", "gui_fragment", pipelineConfig, new PipelineUIProvider());
  }

  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<UIUniformObject>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.setLayoutCount = (uint)layouts.Length;
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _textureSetLayout?.Dispose();
    _setLayout?.Dispose();

    _uiBuffer?.Dispose();

    _descriptorPool?.FreeDescriptors(_descriptorSets);
    _descriptorPool?.Dispose();

    _texturePool?.FreeDescriptors(_textureSets);
    _texturePool?.Dispose();

    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}