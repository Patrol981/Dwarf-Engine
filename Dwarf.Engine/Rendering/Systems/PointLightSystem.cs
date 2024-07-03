using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.EntityComponentSystem.Lightning;
using Dwarf.Rendering.Lightning;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Systems;

public class PointLightSystem : SystemBase {
  private Entity[] _lightsCache = [];
  private readonly unsafe PointLightPushConstant* _lightPushConstant =
    (PointLightPushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<PointLightPushConstant>());

  public PointLightSystem(
    IDevice device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(device, renderer, globalSetLayout, configInfo) {
    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData<PointLightPushConstant>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "point_light_vertex",
      FragmentName = "point_light_fragment",
      PipelineProvider = new PipelinePointLightProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public void Setup() {
    _device.WaitQueue();
  }

  public unsafe void Update(ref FrameInfo frameInfo, ref GlobalUniformBufferObject ubo, ReadOnlySpan<Entity> entities) {
    var lights = entities.DistinctReadOnlySpan<PointLightComponent>();

    if (lights.Length > 0) {
      _lightsCache = lights.ToArray();
    } else {
      Array.Clear(_lightsCache);
      return;
    }

    // PointLight* lightData = stackalloc PointLight[128];
    var lightData = new PointLight[128];
    // var lightData = new UnmanagedArray<PointLight>()

    // Logger.Info($"{ubo->PointLightsLength}");

    for (int i = 0; i < lights.Length; i++) {
      var pos = lights[i].GetComponent<Transform>();
      // ubo->PointLights[i].LightPosition = new Vector4(pos.Position, 1.0f);
      lightData[i].LightPosition = new Vector4(1, 2, 3, 4);
      lightData[i].LightColor = lights[i].GetComponent<PointLightComponent>().Color;

      // Logger.Info($"setting index {i} with values {lightData[i].LightPosition}");
    }

    // var unmanagedData = new UnmanagedArray<PointLight>(lightData);
    ubo.PointLightsLength = lights.Length;
    ubo.PointLights = lightData;
    // ubo.PointLights = (PointLight*)unmanagedData.Handle;
  }

  public void Render(FrameInfo frameInfo) {
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

    for (int i = 0; i < _lightsCache.Length; i++) {
      var light = _lightsCache[i].GetComponent<PointLightComponent>();
      var pos = _lightsCache[i].GetComponent<Transform>();
      unsafe {
        _lightPushConstant->Color = light.Color;
        _lightPushConstant->Position = new Vector4(pos.Position, 1.0f);
        _lightPushConstant->Radius = pos.Scale.X;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<PointLightPushConstant>(),
          _lightPushConstant
        );

        vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    MemoryUtils.FreeIntPtr<PointLightPushConstant>((nint)_lightPushConstant);

    base.Dispose();
  }
}