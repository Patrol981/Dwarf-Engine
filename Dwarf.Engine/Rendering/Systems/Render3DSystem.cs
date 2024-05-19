using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class Render3DSystem : SystemBase, IRenderSystem {
  public const string Simple3D = "simple3D";
  public const string Skinned3D = "skinned3D";

  private DwarfBuffer _modelBuffer = null!;

  private VkDescriptorSet _dynamicSet = VkDescriptorSet.Null;
  private VulkanDescriptorWriter _dynamicWriter = null!;

  private readonly DescriptorSetLayout _jointDescriptorLayout = null!;

  // private readonly List<VkDrawIndexedIndirectCommand> _indirectCommands = [];
  // private readonly DwarfBuffer _indirectCommandBuffer = null!;

  private ModelUniformBufferObject _modelUbo = new();
  private readonly unsafe SimplePushConstantData* _pushConstantData =
    (SimplePushConstantData*)Marshal.AllocHGlobal(Unsafe.SizeOf<SimplePushConstantData>());

  public Render3DSystem(
    IDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    _setLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.UniformBufferDynamic, VkShaderStageFlags.AllGraphics)
      .Build();

    _jointDescriptorLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.StorageBuffer, VkShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
    .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    .Build();

    VkDescriptorSetLayout[] basicLayouts = [
      _textureSetLayout.GetDescriptorSetLayout(),
      globalSetLayout,
      _setLayout.GetDescriptorSetLayout(),
    ];

    VkDescriptorSetLayout[] complexLayouts = [
      .. basicLayouts,
      _jointDescriptorLayout.GetDescriptorSetLayout(),
    ];

    AddPipelineData<SimplePushConstantData>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "vertex",
      FragmentName = "fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = basicLayouts,
      PipelineName = Simple3D
    });

    AddPipelineData<SimplePushConstantData>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "vertex_skinned",
      FragmentName = "fragment_skinned",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = complexLayouts,
      PipelineName = Skinned3D
    });
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

  private void BuildTargetDescriptorTexture(Entity entity, ref TextureManager textures, int modelPart = 0) {
    var target = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
    var id = target!.GetTextureIdReference(modelPart);
    var texture = (VulkanTexture)textures.GetTexture(id);
    if (texture == null) {
      var nid = textures.GetTextureId("./Resources/Textures/base/no_texture.png");
      texture = (VulkanTexture)textures.GetTexture(nid);
    }

    texture.BuildDescriptor(_textureSetLayout, _descriptorPool);
  }

  private void BuildTargetDescriptorJointBuffer(Mesh mesh) {
    if (mesh.Skin == null) return;

    mesh.Skin.BuildDescriptor(_jointDescriptorLayout, _descriptorPool);
  }

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    _device.WaitQueue();
    var startTime = DateTime.Now;

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 3D");

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(3000)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 1000)
      .AddPoolSize(VkDescriptorType.UniformBufferDynamic, 1000)
      .AddPoolSize(VkDescriptorType.StorageBuffer, 1000)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = CalculateLengthOfPool(entities);

    // entities.length before, param no.3
    _modelBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
      (ulong)_texturesCount,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible,
      ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
    );

    for (int i = 0; i < entities.Length; i++) {
      var targetModel = entities[i].GetDrawable<IRender3DElement>() as IRender3DElement;
      if (targetModel!.IsSkinned) {
        targetModel.BuildDescriptors(_jointDescriptorLayout, _descriptorPool);
      }

      if (targetModel!.MeshsesCount > 1) {
        for (int x = 0; x < targetModel.MeshsesCount; x++) {
          BuildTargetDescriptorTexture(entities[i], ref textures, x);
          BuildTargetDescriptorJointBuffer(targetModel.Meshes[x]);
        }
      } else {
        BuildTargetDescriptorTexture(entities[i], ref textures);
        BuildTargetDescriptorJointBuffer(targetModel.Meshes[0]);
      }
    }

    var range = _modelBuffer.GetDescriptorBufferInfo(_modelBuffer.GetAlignmentSize());
    range.range = _modelBuffer.GetAlignmentSize();
    unsafe {
      _dynamicWriter = new VulkanDescriptorWriter(_setLayout, _descriptorPool);
      _dynamicWriter.WriteBuffer(0, &range);
      _dynamicWriter.Build(out _dynamicSet);
    }

    var endTime = DateTime.Now;
    Logger.Warn($"[RENDER 3D RELOAD TIME]: {(endTime - startTime).TotalMilliseconds}");
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (_modelBuffer == null) {
      var textureManager = Application.Instance.TextureManager;
      Setup(entities, ref textureManager);
    }
    if (entities.Length > (int)_modelBuffer!.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (int)_modelBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = CalculateLengthOfPool(entities);
    return len == _texturesCount;
  }

  public void Render(FrameInfo frameInfo, Span<IRender3DElement> entities) {
    if (entities.Length < 1) return;

    var skinnedEntities = entities.ToArray().Where(x => x.IsSkinned);
    var notSkinnedEntities = entities.ToArray().Where(x => !x.IsSkinned);

    RenderSimple(frameInfo, notSkinnedEntities.ToArray());
    RenderComplex(frameInfo, skinnedEntities.ToArray(), notSkinnedEntities.Count());
  }

  private void RenderSimple(FrameInfo frameInfo, Span<IRender3DElement> entities) {
    BindPipeline(frameInfo.CommandBuffer, Simple3D);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Simple3D].PipelineLayout,
        1,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );
    }

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    IRender3DElement lastModel = null!;

    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].GetOwner().CanBeDisposed) continue;

      var materialData = entities[i].GetOwner().GetComponent<Material>().Data;

      // _modelUbo.Material = materialData;
      _modelUbo.Color = materialData.Color;
      _modelUbo.Specular = materialData.Specular;
      _modelUbo.Shininess = materialData.Shininess;
      _modelUbo.Diffuse = materialData.Diffuse;
      _modelUbo.Ambient = materialData.Ambient;

      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * (uint)i;

      ulong offset = 0;
      unsafe {
        var fixedSize = _modelBuffer.GetAlignmentSize() / 2;

        offset = fixedSize * (ulong)(i);
        fixed (ModelUniformBufferObject* modelUboPtr = &_modelUbo) {
          _modelBuffer.WriteToBuffer((IntPtr)(modelUboPtr), _modelBuffer.GetInstanceSize(), offset);
        }
      }

      var transform = entities[i].GetOwner().GetComponent<Transform>();

      unsafe {
        _pushConstantData->ModelMatrix = transform.Matrix4;
        _pushConstantData->NormalMatrix = transform.NormalMatrix;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          _pipelines[Simple3D].PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<SimplePushConstantData>(),
          _pushConstantData
        );

        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelines[Simple3D].PipelineLayout,
            2,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      if (!entities[i].GetOwner().CanBeDisposed && entities[i].GetOwner().Active) {
        for (uint x = 0; x < entities[i].MeshsesCount; x++) {
          if (!entities[i].FinishedInitialization) continue;

          if (i == _texturesCount) continue;
          var targetTexture = frameInfo.TextureManager.GetTexture(entities[i].GetTextureIdReference((int)x));
          Descriptor.BindDescriptorSet(
            targetTexture.TextureDescriptor,
            frameInfo,
            _pipelines[Simple3D].PipelineLayout,
            0,
            1
          );

          if (entities[i] != lastModel)
            entities[i].Bind(frameInfo.CommandBuffer, x);

          entities[i].Draw(frameInfo.CommandBuffer, x);

          entities[i].Meshes[x].Skin?.Ssbo.Unmap();
        }
      }

      lastModel = entities[i];
    }

    _modelBuffer.Unmap();
  }

  private void RenderComplex(FrameInfo frameInfo, Span<IRender3DElement> entities, int prevIdx) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        1,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );
    }

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    IRender3DElement lastModel = null!;

    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].GetOwner().CanBeDisposed) continue;

      var materialData = entities[i].GetOwner().GetComponent<Material>().Data;

      // _modelUbo.Material = materialData;
      _modelUbo.Color = materialData.Color;
      _modelUbo.Specular = materialData.Specular;
      _modelUbo.Shininess = materialData.Shininess;
      _modelUbo.Diffuse = materialData.Diffuse;
      _modelUbo.Ambient = materialData.Ambient;

      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * ((uint)i + (uint)prevIdx);

      ulong offset = 0;
      unsafe {
        var fixedSize = _modelBuffer.GetAlignmentSize() / 2;

        offset = fixedSize * (ulong)(i + prevIdx);
        fixed (ModelUniformBufferObject* modelUboPtr = &_modelUbo) {
          _modelBuffer.WriteToBuffer((IntPtr)(modelUboPtr), _modelBuffer.GetInstanceSize(), offset);
        }
      }

      var transform = entities[i].GetOwner().GetComponent<Transform>();

      unsafe {
        _pushConstantData->ModelMatrix = transform.Matrix4;
        _pushConstantData->NormalMatrix = transform.NormalMatrix;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          _pipelines[Skinned3D].PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<SimplePushConstantData>(),
          _pushConstantData
        );

        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelines[Skinned3D].PipelineLayout,
            2,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      if (!entities[i].GetOwner().CanBeDisposed && entities[i].GetOwner().Active) {
        for (uint x = 0; x < entities[i].MeshsesCount; x++) {
          if (!entities[i].FinishedInitialization) continue;

          if (entities[i].IsSkinned) {
            // Logger.Info($"Inv Len : {entities[i].Meshes[x].Skin!.InverseBindMatrices.Length}");
            // Logger.Info($"Mesh Len : {entities[i].Meshes.Length}");
            for (int y = 0; y < entities[i].Meshes[x].Skin!.InverseBindMatrices.Length; y++) {
              var target = entities[i].Meshes[x].Skin!.InverseBindMatrices[y];

              // entities[i].Meshes[x].Skin?.Ssbo.Flush();
              entities[i].Meshes[x].Skin?.Ssbo.Map(
                (ulong)Unsafe.SizeOf<Matrix4x4>(),
                (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)y
              );

              var test = Matrix4x4.CreateTranslation(new Vector3(0, -y, 0));

              entities[i].Meshes[x].Skin?.Write(
                test,
                (ulong)Unsafe.SizeOf<Matrix4x4>(),
                (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)y
              );

              // Ssbo.WriteToBuffer((nint)(&data), size, offset);
              /*
              unsafe {
                fixed (Matrix4x4* inverseMatricesPtr = entities[i].InverseMatrices) {
                  entities[i].Ssbo.WriteToBuffer(
                    (nint)inverseMatricesPtr,
                    entities[i].Ssbo.GetAlignmentSize()
                  );
                }
              }
              */

            }

            Descriptor.BindDescriptorSet(
              entities[i].SkinDescriptor,
              frameInfo,
              _pipelines[Skinned3D].PipelineLayout,
              3,
              1
            );
          }

          if (i == _texturesCount) continue;
          var targetTexture = frameInfo.TextureManager.GetTexture(entities[i].GetTextureIdReference((int)x));
          Descriptor.BindDescriptorSet(targetTexture.TextureDescriptor, frameInfo, PipelineLayout, 0, 1);

          if (entities[i] != lastModel)
            entities[i].Bind(frameInfo.CommandBuffer, x);

          entities[i].Draw(frameInfo.CommandBuffer, x);

          entities[i].Meshes[x].Skin?.Ssbo.Unmap();
        }
      }

      lastModel = entities[i];
    }

    _modelBuffer.Unmap();
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    MemoryUtils.FreeIntPtr<SimplePushConstantData>((nint)_pushConstantData);

    _modelBuffer?.Dispose();
    _descriptorPool?.FreeDescriptors([_dynamicSet]);

    _jointDescriptorLayout?.Dispose();

    base.Dispose();
  }
}