using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vortice.Vulkan;

namespace Dwarf.Engine.Rendering;
public interface IDrawable : IDisposable {
  public void Bind(VkCommandBuffer commandBuffer);
  public void Draw(VkCommandBuffer commandBuffer);
}
