using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vortice.Vulkan;

namespace Dwarf.Engine.Rendering.UI;
public interface IUIElement : IDrawable {
  public void Update();
  public void BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout);
  public Guid GetTextureIdReference();
  public void DrawText(string text);
}
