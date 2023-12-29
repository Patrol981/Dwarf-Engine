using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vortice.Vulkan;

namespace DwarfEngine.Vulkan;
public abstract class PipelineProvider {
  public unsafe virtual VkVertexInputBindingDescription* GetBindingDescsFunc() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }

  public unsafe virtual VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }

  public virtual uint GetBindingsLength() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }

  public virtual uint GetAttribsLength() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }
}
