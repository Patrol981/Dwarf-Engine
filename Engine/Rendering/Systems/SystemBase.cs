using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.Rendering;
using Dwarf.Vulkan;

using DwarfEngine.Engine;

using Vortice.Vulkan;

namespace Dwarf.Engine;
public abstract class SystemBase {
  public abstract IRenderSystem Create(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSet,
    PipelineConfigInfo configInfo = null!
  );
}
