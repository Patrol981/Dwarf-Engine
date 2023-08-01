using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vortice.Vulkan;

namespace Dwarf.Engine.Rendering;
public interface IRender3DElement : IDrawable {
  public bool UsesTexture { get; }
  public bool UsesLight { get; set; }
  public int MeshsesCount { get; }
  public bool FinishedInitialization { get; }
  public void BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout);
}
