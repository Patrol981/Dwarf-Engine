using System.Runtime.CompilerServices;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Vulkan;
using OpenTK.Mathematics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public unsafe class SimpleRenderSystem : IDisposable {
  private readonly Device _device = null!;
  private readonly Renderer _renderer = null!;
  private PipelineConfigInfo _configInfo = null!;
  private Pipeline _pipeline = null!;
  private VkPipelineLayout _pipelineLayout;

  // Uniforms
  private Vulkan.Buffer[] _modelBuffer = new Vulkan.Buffer[0];
  private DescriptorPool _pool = null!;
  private DescriptorPool _texturePool = null!;
  private DescriptorSetLayout _setLayout = null!;
  private DescriptorSetLayout _textureSetLayout = null!;
  private VkDescriptorSet[] _descriptorSets = new VkDescriptorSet[0];
  private VkDescriptorSet[] _textureSets = new VkDescriptorSet[0];

  public SimpleRenderSystem(
    Device device,
    Renderer renderer,
    VkRenderPass renderPass,
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
    CreatePipeline(renderPass);
  }

  private int GetLengthOfTexturedEntites(ReadOnlySpan<Entity> entities) {
    int count = 0;
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].GetComponent<Model>().UsesTexture) {
        count++;
      }
    }
    return count;
  }

  public void SetupRenderData(ReadOnlySpan<Entity> entities, TextureManager textures) {
    _pool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)entities.Length)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)entities.Length)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturePool = new DescriptorPool.Builder(_device)
    .SetMaxSets((uint)entities.Length)
    .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)entities.Length)
    .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
    .Build();

    _modelBuffer = new Vulkan.Buffer[entities.Length];
    _descriptorSets = new VkDescriptorSet[entities.Length];
    _textureSets = new VkDescriptorSet[entities.Length];

    for (int i = 0; i < entities.Length; i++) {
      _modelBuffer[i] = new Vulkan.Buffer(
        _device,
        (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
        1,
        VkBufferUsageFlags.UniformBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
        _device.Properties.limits.minUniformBufferOffsetAlignment
      );

      if (entities[i].GetComponent<Model>().UsesTexture) {

        var id = entities[i].GetComponent<Model>().TextureIdReference;
        var texture = textures.GetTexture(id);
        VkDescriptorImageInfo imageInfo = new();
        imageInfo.sampler = texture.GetSampler();
        imageInfo.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
        imageInfo.imageView = texture.GetImageView();
        // var imageInfo = _modelBuffer[i].get
        VkDescriptorSet set;
        var texWriter = new DescriptorWriter(_textureSetLayout, _texturePool)
          .WriteImage(0, &imageInfo)
          .Build(out set);

        _textureSets[i] = set;
      }
      var bufferInfo = _modelBuffer[i].GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<ModelUniformBufferObject>());
      var writer = new DescriptorWriter(_setLayout, _pool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
    }
  }

  public bool CheckSizes(int numberOfEntities) {
    if (numberOfEntities > _modelBuffer.Length) {
      return false;
    } else if (numberOfEntities < _modelBuffer.Length) {
      return true;
    }
    return true;
  }

  public void RenderEntities(FrameInfo frameInfo, Span<Entity> entities) {
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
      var modelUBO = new ModelUniformBufferObject();
      modelUBO.ModelMatrix = entities[i].GetComponent<Transform>().Matrix4;
      modelUBO.NormalMatrix = entities[i].GetComponent<Transform>().NormalMatrix;
      modelUBO.Material = entities[i].GetComponent<Material>().GetColor();
      modelUBO.UseTexture = entities[i].GetComponent<Model>().UsesTexture;

      _modelBuffer[i].Map((ulong)Unsafe.SizeOf<ModelUniformBufferObject>());
      _modelBuffer[i].WriteToBuffer((IntPtr)(&modelUBO), (ulong)Unsafe.SizeOf<ModelUniformBufferObject>());
      _modelBuffer[i].Unmap();

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

      if (entities[i].GetComponent<Model>().UsesTexture) {
        fixed (VkDescriptorSet* ptr = &_textureSets[i]) {
          vkCmdBindDescriptorSets(
           frameInfo.CommandBuffer,
           VkPipelineBindPoint.Graphics,
           _pipelineLayout,
           2,
           1,
           ptr,
           0,
           null
         );
        }
      }

      var entity = entities[i].GetComponent<Model>();
      if (!entity.Owner!.CanBeDisposed) {
        for (uint x = 0; x < entity.MeshsesCount; x++) {
          entity.Bind(frameInfo.CommandBuffer, x);
          entity.Draw(frameInfo.CommandBuffer, x);
        }
      }
    }
  }

  public void SetPipelineConfigInfo(PipelineConfigInfo configInfo) {
    _configInfo = configInfo;
  }

  public PipelineConfigInfo GetPipelineConfigInfo() {
    return _configInfo;
  }

  private void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<SimplePushConstantData>();

    // VkDescriptorSetLayout[] descriptorSetLayouts = new VkDescriptorSetLayout[] { globalSetLayout };

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
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
    // PipelineConfigInfo configInfo = new();
    if (_configInfo == null) {
      _configInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _configInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "vertex", "fragment", pipelineConfig);
  }

  public void Dispose() {
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    for (int i = 0; i < _modelBuffer.Length; i++) {
      _modelBuffer[i]?.Dispose();
    }
    _pool?.FreeDescriptors(_descriptorSets);
    _pool?.Dispose();
    _texturePool?.FreeDescriptors(_textureSets);
    _texturePool?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}