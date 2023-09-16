using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.Rendering;
using Dwarf.Extensions.Lists;
using Dwarf.Vulkan;

using Dwarf.Engine;

using Vortice.Vulkan;

namespace Dwarf.Engine;
public abstract class SystemBase
{
  protected readonly Device _device = null!;
  protected readonly Renderer _renderer = null!;
  protected VkDescriptorSetLayout _globalDescriptorSetLayout;
  protected PipelineConfigInfo _pipelineConfigInfo;
  protected VkPipelineLayout _pipelineLayout;
  protected Pipeline _pipeline = null!;

  // protected Vulkan.Buffer[] _buffer = new Vulkan.Buffer[0];
  protected DescriptorPool _descriptorPool = null!;
  protected DescriptorPool _texturePool = null!;
  protected DescriptorSetLayout _setLayout = null!;
  protected DescriptorSetLayout _textureSetLayout = null!;
  protected VkDescriptorSet[] _descriptorSets = new VkDescriptorSet[0];

  protected int _texturesCount = 0;

  public SystemBase(
    Device device,
    Renderer renderer,
    VkDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  )
  {
    _device = device;
    _renderer = renderer;
    _globalDescriptorSetLayout = globalSetLayout;
    _pipelineConfigInfo = configInfo;
  }
}
