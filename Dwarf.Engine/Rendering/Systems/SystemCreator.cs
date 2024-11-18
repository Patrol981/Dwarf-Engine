using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

namespace Dwarf.Rendering;

[Flags]
public enum SystemCreationFlags {
  None = 0,
  Renderer3D = 1,
  Renderer2D = 1 << 1,
  RendererUI = 1 << 2,
  Physics3D = 1 << 3,
  DirectionalLight = 1 << 4,
  PointLights = 1 << 5,
  Guizmos = 1 << 6,
  WebApi = 1 << 7,
}

public record SystemConfiguration {
  public Dwarf.Physics.Backends.BackendKind PhysiscsBackend { get; init; }

  public static SystemConfiguration Default => new() {
    PhysiscsBackend = Physics.Backends.BackendKind.Default,
  };

  public static SystemConfiguration GetDefault() => new() {
    PhysiscsBackend = Physics.Backends.BackendKind.Default,
  };
}

public class SystemCreator {
  public static void CreateSystems(
    SystemCollection systemCollection,
    SystemCreationFlags flags,
    SystemConfiguration systemConfig,
    VulkanDevice device,
    Renderer renderer,
    Dictionary<string, DescriptorSetLayout> layouts,
    PipelineConfigInfo configInfo = null!
  ) {
    var hasRenderer3D = flags.HasFlag(SystemCreationFlags.Renderer3D);
    var hasRenderer2D = flags.HasFlag(SystemCreationFlags.Renderer2D);
    var hasRendererUI = flags.HasFlag(SystemCreationFlags.RendererUI);
    var usePhysics3D = flags.HasFlag(SystemCreationFlags.Physics3D);
    var hasDirectionalLight = flags.HasFlag(SystemCreationFlags.DirectionalLight);
    var hasPointLights = flags.HasFlag(SystemCreationFlags.PointLights);
    var hasGuizmos = flags.HasFlag(SystemCreationFlags.Guizmos);
    var hasWebApi = flags.HasFlag(SystemCreationFlags.WebApi);

    if (hasRendererUI) {
      Logger.Info("[SYSTEM CREATOR] Creating UI Renderer");
      systemCollection.RenderUISystem =
        new(device, renderer, layouts["Global"].GetDescriptorSetLayout(), configInfo);
    }
    if (hasRenderer3D) {
      Logger.Info("[SYSTEM CREATOR] Creating 3D Renderer");
      systemCollection.Render3DSystem =
        new(device, renderer, layouts, configInfo);

      Logger.Info("[SYSTEM CREATOR] Creating 3D Debug Renderer");
      var debugConfig = new VertexDebugPipeline();
      systemCollection.RenderDebugSystem =
        new(device, renderer, layouts["Global"].GetDescriptorSetLayout(), debugConfig);
    }
    if (hasRenderer2D) {
      Logger.Info("[SYSTEM CREATOR] Creating 2D Renderer");
      systemCollection.Render2DSystem =
        new(device, renderer, layouts["Global"].GetDescriptorSetLayout(), configInfo);
    }
    if (usePhysics3D) {
      Logger.Info("[SYSTEM CREATOR] Setting up Physics 3D");
      systemCollection.PhysicsSystem = new(systemConfig.PhysiscsBackend);
    }
    if (hasDirectionalLight) {
      Logger.Info("[SYSTEM CREATOR] Creating Directional Light System");
      systemCollection.DirectionalLightSystem =
        new(device, renderer, layouts["Global"].GetDescriptorSetLayout());
    }
    if (hasPointLights) {
      Logger.Info("[SYSTEM CREATOR] Creating Point Light System");
      systemCollection.PointLightSystem =
        new(device, renderer, layouts["Global"].GetDescriptorSetLayout());
    }
    if (hasGuizmos) {
      Logger.Info("[SYSTEM CREATOR] Creating Guizmos Rendering System");
      systemCollection.GuizmoRenderSystem =
        new(device, renderer, layouts["Global"].GetDescriptorSetLayout());
    }
    if (hasWebApi) {
      Logger.Info("[SYSTEM CREATOR] Creating WebApi");
      systemCollection.WebApi = new(app: Application.Instance);
    }
  }
}
