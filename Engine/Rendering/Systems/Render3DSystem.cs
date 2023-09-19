using System.Runtime.CompilerServices;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using Dwarf.Engine;

using OpenTK.Mathematics;

using Vortice.Vulkan;

using static System.Runtime.InteropServices.JavaScript.JSType;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;

public class Render3DSystem : SystemBase, IRenderSystem {
  private PublicList<PublicList<VkDescriptorSet>> _textureSets = new();
  private Vulkan.Buffer _modelBuffer;

  private VkDescriptorSet _dynamicSet;
  private DescriptorWriter _dynamicWriter;

  public Render3DSystem(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBufferDynamic, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
    .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = new VkDescriptorSetLayout[] {
      _textureSetLayout.GetDescriptorSetLayout(),
      globalSetLayout,
      _setLayout.GetDescriptorSetLayout()
    };
    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(renderer.GetSwapchainRenderPass());
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

  private int CalculateLengthOfPool(ReadOnlySpan<Entity> entities) {
    int count = 0;
    for (int i = 0; i < entities.Length; i++) {
      var targetItems = entities[i].GetDrawables<IRender3DElement>();
      for (int j = 0; j < targetItems.Length; j++) {
        var t = targetItems[j] as IRender3DElement;
        count += t!.MeshsesCount;
      }

    }
    return count;
  }

  private int GetTextureSetsLength() {
    int count = 0;
    for (int i = 0; i < _textureSets.Size; i++) {
      count += _textureSets.GetAt(i).Size;
    }
    return count;
  }

  private void BindDescriptorTexture(Entity entity, ref TextureManager textures, int index, int modelPart = 0) {
    var id = entity.GetComponent<Model>().GetTextureIdReference(modelPart);
    var texture = textures.GetTexture(id);
    if (texture == null) {
      var nid = textures.GetTextureId("./Textures/base/no_texture.png");
      texture = textures.GetTexture(nid);
    }
    VkDescriptorImageInfo imageInfo = new();
    imageInfo.sampler = texture.GetSampler();
    imageInfo.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
    imageInfo.imageView = texture.GetImageView();
    VkDescriptorSet set;
    unsafe {
      var texWriter = new DescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out set);
    }

    _textureSets.GetAt(index).SetAt(set, modelPart);
  }

  public void SetupRenderData(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    var startTime = DateTime.Now;
    // TODO: Reuse data from diffrent renders?

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 3D");

    _descriptorPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(1000)
      .AddPoolSize(VkDescriptorType.UniformBufferDynamic, 1000)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = CalculateLengthOfPool(entities);

    _texturePool = new DescriptorPool.Builder(_device)
      .SetMaxSets((uint)_texturesCount)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    // _descriptorSets = new VkDescriptorSet[entities.Length];
    _textureSets = new();

    for (int x = 0; x < entities.Length; x++) {
      var en = entities[x].GetComponent<Model>();
      _textureSets.Add(new());
      for (int y = 0; y < en.MeshsesCount; y++) {
        _textureSets.GetAt(x).Add(new());
      }
    }

    _modelBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
      (ulong)entities.Length,
      VkBufferUsageFlags.UniformBuffer,
      VkMemoryPropertyFlags.HostVisible,
      // VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
      _device.Properties.limits.minUniformBufferOffsetAlignment
    );

    for (int i = 0; i < entities.Length; i++) {
      var targetModel = entities[i].GetComponent<Model>();
      if (targetModel.MeshsesCount > 1 && targetModel.UsesTexture) {
        for (int x = 0; x < targetModel.MeshsesCount; x++) {
          BindDescriptorTexture(targetModel.Owner!, ref textures, i, x);
        }
      } else {
        BindDescriptorTexture(targetModel.Owner!, ref textures, i, 0);
      }

      /*
      // var bufferInfo = _modelBuffer.GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<ModelUniformBufferObject>());
      var bufferInfo = _modelBuffer.GetVkDescriptorBufferInfoForIndex(i);
      unsafe {
        var writer = new DescriptorWriter(_setLayout, _descriptorPool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
      }
      */
    }

    var range = _modelBuffer.GetDescriptorBufferInfo(_modelBuffer.GetAlignmentSize());
    range.range = _modelBuffer.GetAlignmentSize();
    unsafe {
      _dynamicWriter = new DescriptorWriter(_setLayout, _descriptorPool);
      _dynamicWriter.WriteBuffer(0, &range);
      _dynamicWriter.Build(out _dynamicSet);

    }

    var endTime = DateTime.Now;
    Logger.Warn($"[RENDER 3D RELOAD TIME]: {(endTime - startTime).TotalMilliseconds}");
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (entities.Length > (int)_modelBuffer.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (int)_modelBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = CalculateLengthOfPool(entities);
    var sets = GetTextureSetsLength();
    if (len != sets) {
      return false;
    }

    return true;
  }

  public async void RenderEntities(FrameInfo frameInfo, Entity[] entities) {
    _pipeline.Bind(frameInfo.CommandBuffer);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelineLayout,
        1,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );
    }

    List<Task> _tasks = new List<Task>();

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    for (int i = 0; i < entities.Length; i++) {
      //var targetEntity = entities[i].GetDrawable<IRender3DElement>() as IRender3DElement;
      var targetEntity = entities[i].GetComponent<Model>();
      var modelUBO = new ModelUniformBufferObject();
      modelUBO.Material = entities[i].GetComponent<Material>().GetColor();
      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * (uint)i;

      unsafe {
        // var map = _modelBuffer.GetMappedMemory();
        // char* memOffset = (char*)map;
        var fixedSize = _modelBuffer.GetAlignmentSize() / 2;
        // memOffset += fixedSize * (ulong)(i);

        var offset = fixedSize * (ulong)(i);
        _modelBuffer.WriteToBuffer((IntPtr)(&modelUBO), _modelBuffer.GetInstanceSize(), offset);
        /*
        VkUtils.MemCopy(
          (IntPtr)memOffset,
          (IntPtr)(&modelUBO),
          (int)_modelBuffer.GetInstanceSize()
        );
        */
      }

      var pushConstantData = new SimplePushConstantData();
      pushConstantData.ModelMatrix = entities[i].GetComponent<Transform>().Matrix4;
      pushConstantData.NormalMatrix = entities[i].GetComponent<Transform>().NormalMatrix;

      unsafe {
        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          _pipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<SimplePushConstantData>(),
          &pushConstantData
        );

        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelineLayout,
            2,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      if (!entities[i].CanBeDisposed && entities[i].Active) {
        for (uint x = 0; x < targetEntity.MeshsesCount; x++) {
          if (!targetEntity.FinishedInitialization) continue;
          if (targetEntity.UsesTexture) {
            // await targetEntity.BindDescriptorSet(_textureSets.GetAt(i).GetAt((int)x), frameInfo, ref _pipelineLayout);
            _tasks.Add(targetEntity.BindDescriptorSet(_textureSets.GetAt(i).GetAt((int)x), frameInfo, ref _pipelineLayout));
          }
          // await targetEntity.Bind(frameInfo.CommandBuffer, x);
          // await targetEntity.Draw(frameInfo.CommandBuffer, x);
          _tasks.Add(targetEntity.Bind(frameInfo.CommandBuffer, x));
          _tasks.Add(targetEntity.Draw(frameInfo.CommandBuffer, x));
        }
      }
    }

    // Parallel.Invoke(_tasks.ToArray());
    await Task.WhenAll(_tasks);
    _modelBuffer.Unmap();
  }

  private void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment;
    pushConstantRange.offset = 0;
    pushConstantRange.size = (uint)Unsafe.SizeOf<SimplePushConstantData>();

    VkPipelineLayoutCreateInfo pipelineInfo = new();
    pipelineInfo.setLayoutCount = (uint)layouts.Length;
    unsafe {
      fixed (VkDescriptorSetLayout* ptr = layouts) {
        pipelineInfo.pSetLayouts = ptr;
      }
      pipelineInfo.pushConstantRangeCount = 1;
      pipelineInfo.pPushConstantRanges = &pushConstantRange;
      vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
    }
  }

  private void CreatePipeline(VkRenderPass renderPass) {
    _pipeline?.Dispose();
    if (_pipelineConfigInfo == null) {
      _pipelineConfigInfo = new PipelineConfigInfo();
    }
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, "vertex", "fragment", pipelineConfig, new PipelineModelProvider());
  }

  public unsafe void Dispose() {
    vkQueueWaitIdle(_device.GraphicsQueue);
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    _modelBuffer?.Dispose();
    _descriptorPool?.FreeDescriptors(new VkDescriptorSet[] { _dynamicSet });
    _descriptorPool?.Dispose();
    _texturePool?.FreeDescriptors(_textureSets);
    _texturePool?.Dispose();
    _pipeline?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
  }
}