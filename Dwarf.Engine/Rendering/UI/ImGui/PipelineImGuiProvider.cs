using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Vulkan;

using ImGuiNET;

using Vortice.Vulkan;

namespace Dwarf.Rendering.UI;
public class PipelineImGuiProvider : PipelineProvider {
  public override unsafe VkVertexInputBindingDescription* GetBindingDescsFunc() {
    var bindingDescriptions = new VkVertexInputBindingDescription[1];
    bindingDescriptions[0].binding = 0;
    bindingDescriptions[0].stride = ((uint)Unsafe.SizeOf<ImDrawVert>());
    bindingDescriptions[0].inputRate = VkVertexInputRate.Vertex;
    fixed (VkVertexInputBindingDescription* ptr = bindingDescriptions) {
      return ptr;
    }
  }

  public override unsafe VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    var attributeDescriptions = new VkVertexInputAttributeDescription[GetAttribsLength()];
    attributeDescriptions[0].binding = 0;
    attributeDescriptions[0].location = 0;
    attributeDescriptions[0].format = VkFormat.R32G32Sfloat;
    attributeDescriptions[0].offset = (uint)Marshal.OffsetOf<ImDrawVert>("pos");

    attributeDescriptions[1].binding = 0;
    attributeDescriptions[1].location = 1;
    attributeDescriptions[1].format = VkFormat.R32G32Sfloat;
    attributeDescriptions[1].offset = (uint)Marshal.OffsetOf<ImDrawVert>("uv");

    attributeDescriptions[2].binding = 0;
    attributeDescriptions[2].location = 2;
    attributeDescriptions[2].format = VkFormat.R8G8B8A8Unorm;
    attributeDescriptions[2].offset = (uint)Marshal.OffsetOf<ImDrawVert>("col");

    fixed (VkVertexInputAttributeDescription* ptr = attributeDescriptions) {
      return ptr;
    }
  }

  public override uint GetAttribsLength() {
    return 3;
  }

  public override uint GetBindingsLength() {
    return 1;
  }
}
