using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Rendering.Systems;
using Dwarf.Vulkan;

namespace Dwarf.Engine.Rendering;

[Flags]
public enum SystemCreationFlags {
  None = 0b0000,
  Renderer3D = 0b0001,
  Renderer2D = 0b0010,
  RendererUI = 0b0100,

  Physics3D = 0b1111
}

public class SystemCreator {
  public static void CreateSystems(
    ref SystemCollection systemCollection,
    SystemCreationFlags flags,
    Device device,
    Renderer renderer,
    DescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) {
    if (flags.HasFlag(SystemCreationFlags.RendererUI)) {
      systemCollection.RenderUISystem = new RenderUISystem(device, renderer, globalSetLayout.GetDescriptorSetLayout(), configInfo);
    }
    if (flags.HasFlag(SystemCreationFlags.Renderer3D)) {
      systemCollection.Render3DSystem = new Render3DSystem(device, renderer, globalSetLayout.GetDescriptorSetLayout(), configInfo);
    }
    if (flags.HasFlag(SystemCreationFlags.Renderer2D)) {
      systemCollection.Render2DSystem = new Render2DSystem(device, renderer, globalSetLayout.GetDescriptorSetLayout(), configInfo);
    }

    if (flags.HasFlag(SystemCreationFlags.Physics3D)) {
      systemCollection.PhysicsSystem = (new Physics.PhysicsSystem());
    }

    var debugConfig = new VertexDebugPipeline();
    systemCollection.RenderDebugSystem = new RenderDebugSystem(device, renderer, globalSetLayout.GetDescriptorSetLayout(), debugConfig);
  }
}
