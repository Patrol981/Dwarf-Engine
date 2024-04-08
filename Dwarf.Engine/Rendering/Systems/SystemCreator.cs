using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

namespace Dwarf.Engine.Rendering;

[Flags]
public enum SystemCreationFlags {
  None = 0,
  Renderer3D = 1,
  Renderer2D = 2,
  RendererUI = 4,
  Physics3D = 8,
  PointLights = 16,
  Guizmos = 32,
}

public class SystemCreator {
  public static void CreateSystems(
    ref SystemCollection systemCollection,
    SystemCreationFlags flags,
    VulkanDevice device,
    Renderer renderer,
    DescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) {
    var hasRenderer3D = flags.HasFlag(SystemCreationFlags.Renderer3D);
    var hasRenderer2D = flags.HasFlag(SystemCreationFlags.Renderer2D);
    var hasRendererUI = flags.HasFlag(SystemCreationFlags.RendererUI);
    var usePhysics3D = flags.HasFlag(SystemCreationFlags.Physics3D);
    var hasPointLights = flags.HasFlag(SystemCreationFlags.PointLights);
    var hasGuizmos = flags.HasFlag(SystemCreationFlags.Guizmos);

    if (hasRendererUI) {
      Logger.Info("[SYSTEM CREATOR] Creating UI Renderer");
      systemCollection.RenderUISystem =
        new(device, renderer, globalSetLayout.GetDescriptorSetLayout(), configInfo);
    }
    if (hasRenderer3D) {
      Logger.Info("[SYSTEM CREATOR] Creating 3D Renderer");
      systemCollection.Render3DSystem =
        new(device, renderer, globalSetLayout.GetDescriptorSetLayout(), configInfo);

      Logger.Info("[SYSTEM CREATOR] Creating 3D Debug Renderer");
      var debugConfig = new VertexDebugPipeline();
      systemCollection.RenderDebugSystem =
        new(device, renderer, globalSetLayout.GetDescriptorSetLayout(), debugConfig);
    }
    if (hasRenderer2D) {
      Logger.Info("[SYSTEM CREATOR] Creating 2D Renderer");
      systemCollection.Render2DSystem =
        new(device, renderer, globalSetLayout.GetDescriptorSetLayout(), configInfo);
    }
    if (usePhysics3D) {
      Logger.Info("[SYSTEM CREATOR] Setting up Physics 3D");
      systemCollection.PhysicsSystem = (new());
    }
    if (hasPointLights) {
      Logger.Info("[SYSTEM CREATOR] Creating Point Light System");
      systemCollection.PointLightSystem =
        new(device, renderer, globalSetLayout.GetDescriptorSetLayout());
    }
    if (hasGuizmos) {
      Logger.Info("[SYSTEM CREATOR] Creating Guizmos Rendering System");
      systemCollection.GuizmoRenderSystem =
        new(device, renderer, globalSetLayout.GetDescriptorSetLayout());
    }
  }
}
