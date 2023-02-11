using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Dwarf.Engine;
using Dwarf.Engine.Windowing;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Dwarf.Extensions.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public struct PipelineConfigInfo {
  public VkPipelineViewportStateCreateInfo ViewportInfo;
  public VkPipelineInputAssemblyStateCreateInfo InputAssemblyInfo;
  public VkPipelineRasterizationStateCreateInfo RasterizationInfo;
  public VkPipelineMultisampleStateCreateInfo MultisampleInfo;
  public VkPipelineColorBlendAttachmentState ColorBlendAttachment;
  public VkPipelineColorBlendStateCreateInfo ColorBlendInfo;
  public VkPipelineDepthStencilStateCreateInfo DepthStencilInfo;

  public VkDynamicState[] DynamicStatesEnables;
  public VkPipelineDynamicStateCreateInfo DynamicStateInfo;

  public VkPipelineLayout PipelineLayout;
  public VkRenderPass RenderPass;
  public uint Subpass;
}

public class Pipeline : IDisposable {
  private readonly Device _device;

  private VkPipeline _graphicsPipeline;
  private VkShaderModule _vertexShaderModule;
  private VkShaderModule _fragmentShaderModule;

  public Pipeline(Device device, string vertexName, string fragmentName, PipelineConfigInfo configInfo) {
    _device = device;

    CreateGraphicsPipeline(vertexName, fragmentName, configInfo);
  }

  public void Bind(VkCommandBuffer commandBuffer) {
    vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
  }

  private unsafe void CreateGraphicsPipeline(string vertexName, string fragmentName, PipelineConfigInfo configInfo) {
    var vertexPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders", $"{vertexName}.spv");
    var fragmentPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders", $"{fragmentName}.spv");
    var vertexCode = File.ReadAllBytes(vertexPath);
    var fragmentCode = File.ReadAllBytes(fragmentPath);

    CreateShaderModule(vertexCode, out _vertexShaderModule);
    CreateShaderModule(fragmentCode, out _fragmentShaderModule);

    VkString entryPoint = new("main");
    VkPipelineShaderStageCreateInfo[] shaderStages = new VkPipelineShaderStageCreateInfo[2];

    //vertex
    shaderStages[0].sType = VkStructureType.PipelineShaderStageCreateInfo;
    shaderStages[0].stage = VkShaderStageFlags.Vertex;
    shaderStages[0].module = _vertexShaderModule;
    shaderStages[0].pName = entryPoint;
    shaderStages[0].flags = 0;
    shaderStages[0].pNext = null;

    //fragment
    shaderStages[1].sType = VkStructureType.PipelineShaderStageCreateInfo;
    shaderStages[1].stage = VkShaderStageFlags.Fragment;
    shaderStages[1].module = _fragmentShaderModule;
    shaderStages[1].pName = entryPoint;
    shaderStages[1].flags = 0;
    shaderStages[1].pNext = null;

    var bindingDescriptions = Model.GetBindingDescsFunc();
    var attributeDescriptions = Model.GetAttribDescsFunc();

    var vertexInputInfo = new VkPipelineVertexInputStateCreateInfo();
    vertexInputInfo.sType = VkStructureType.PipelineVertexInputStateCreateInfo;
    vertexInputInfo.vertexAttributeDescriptionCount = Model.GetAttribsLength();
    vertexInputInfo.vertexBindingDescriptionCount = Model.GetBindingsLength();
    vertexInputInfo.pVertexAttributeDescriptions = attributeDescriptions;
    vertexInputInfo.pVertexBindingDescriptions = bindingDescriptions;

    var pipelineInfo = new VkGraphicsPipelineCreateInfo();
    pipelineInfo.sType = VkStructureType.GraphicsPipelineCreateInfo;
    pipelineInfo.stageCount = 2;
    fixed (VkPipelineShaderStageCreateInfo* ptr = shaderStages) {
      pipelineInfo.pStages = ptr;
    }
    pipelineInfo.pVertexInputState = &vertexInputInfo;

    pipelineInfo.pInputAssemblyState = &configInfo.InputAssemblyInfo;
    pipelineInfo.pViewportState = &configInfo.ViewportInfo;
    pipelineInfo.pRasterizationState = &configInfo.RasterizationInfo;
    pipelineInfo.pMultisampleState = &configInfo.MultisampleInfo;
    pipelineInfo.pColorBlendState = &configInfo.ColorBlendInfo;
    pipelineInfo.pDepthStencilState = &configInfo.DepthStencilInfo;
    pipelineInfo.pDynamicState = &configInfo.DynamicStateInfo;

    pipelineInfo.layout = configInfo.PipelineLayout;
    pipelineInfo.renderPass = configInfo.RenderPass;
    pipelineInfo.subpass = configInfo.Subpass;

    pipelineInfo.basePipelineIndex = -1;
    pipelineInfo.basePipelineHandle = VkPipeline.Null;

    VkPipeline graphicsPipeline = VkPipeline.Null;

    var result = vkCreateGraphicsPipelines(
      _device.LogicalDevice,
      VkPipelineCache.Null,
      1,
      &pipelineInfo,
      null,
      &graphicsPipeline
    );
    if (result != VkResult.Success) throw new Exception("Failed to create graphics pipeline");

    _graphicsPipeline = graphicsPipeline;
  }

  private unsafe void CreateShaderModule(byte[] data, out VkShaderModule module) {
    vkCreateShaderModule(_device.LogicalDevice, data, null, out module).CheckResult();
  }

  public unsafe static PipelineConfigInfo DefaultConfigInfo(PipelineConfigInfo configInfo) {
    configInfo.InputAssemblyInfo.sType = VkStructureType.PipelineInputAssemblyStateCreateInfo;
    configInfo.InputAssemblyInfo.topology = VkPrimitiveTopology.TriangleList;
    configInfo.InputAssemblyInfo.primitiveRestartEnable = false;

    configInfo.ViewportInfo.sType = VkStructureType.PipelineViewportStateCreateInfo;
    configInfo.ViewportInfo.viewportCount = 1;
    configInfo.ViewportInfo.pViewports = null;
    configInfo.ViewportInfo.scissorCount = 1;
    configInfo.ViewportInfo.pScissors = null;

    configInfo.RasterizationInfo.sType = VkStructureType.PipelineRasterizationStateCreateInfo;
    configInfo.RasterizationInfo.depthClampEnable = false;
    configInfo.RasterizationInfo.rasterizerDiscardEnable = false;
    configInfo.RasterizationInfo.polygonMode = VkPolygonMode.Fill;
    configInfo.RasterizationInfo.lineWidth = 1.0f;
    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.None;
    configInfo.RasterizationInfo.frontFace = VkFrontFace.Clockwise;
    configInfo.RasterizationInfo.depthBiasEnable = false;
    configInfo.RasterizationInfo.depthBiasConstantFactor = 0.0f;  // Optional
    configInfo.RasterizationInfo.depthBiasClamp = 0.0f;           // Optional
    configInfo.RasterizationInfo.depthBiasSlopeFactor = 0.0f;     // Optional

    configInfo.MultisampleInfo.sType = VkStructureType.PipelineMultisampleStateCreateInfo;
    configInfo.MultisampleInfo.sampleShadingEnable = false;
    configInfo.MultisampleInfo.rasterizationSamples = VkSampleCountFlags.Count1;
    configInfo.MultisampleInfo.minSampleShading = 1.0f;           // Optional
    configInfo.MultisampleInfo.pSampleMask = null;             // Optional
    configInfo.MultisampleInfo.alphaToCoverageEnable = false;  // Optional
    configInfo.MultisampleInfo.alphaToOneEnable = false;       // Optional

    configInfo.ColorBlendAttachment.colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.B | VkColorComponentFlags.B | VkColorComponentFlags.A;
    configInfo.ColorBlendAttachment.blendEnable = false;
    configInfo.ColorBlendAttachment.srcColorBlendFactor = VkBlendFactor.One;   // Optional
    configInfo.ColorBlendAttachment.dstColorBlendFactor = VkBlendFactor.DstAlpha;  // Optional
    configInfo.ColorBlendAttachment.colorBlendOp = VkBlendOp.Add;              // Optional
    configInfo.ColorBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;   // Optional
    configInfo.ColorBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;  // Optional
    configInfo.ColorBlendAttachment.alphaBlendOp = VkBlendOp.Add;              // Optional

    configInfo.ColorBlendInfo.sType = VkStructureType.PipelineColorBlendStateCreateInfo;
    configInfo.ColorBlendInfo.logicOpEnable = false;
    configInfo.ColorBlendInfo.logicOp = VkLogicOp.Copy;  // Optional
    configInfo.ColorBlendInfo.attachmentCount = 1;
    configInfo.ColorBlendInfo.pAttachments = &configInfo.ColorBlendAttachment;
    configInfo.ColorBlendInfo.blendConstants[0] = 0.0f;  // Optional
    configInfo.ColorBlendInfo.blendConstants[1] = 0.0f;  // Optional
    configInfo.ColorBlendInfo.blendConstants[2] = 0.0f;  // Optional
    configInfo.ColorBlendInfo.blendConstants[3] = 0.0f;  // Optional

    configInfo.DepthStencilInfo.sType = VkStructureType.PipelineDepthStencilStateCreateInfo;
    configInfo.DepthStencilInfo.depthTestEnable = true;
    configInfo.DepthStencilInfo.depthWriteEnable = true;
    configInfo.DepthStencilInfo.depthCompareOp = VkCompareOp.Less;
    configInfo.DepthStencilInfo.depthBoundsTestEnable = false;
    configInfo.DepthStencilInfo.minDepthBounds = 0.0f;  // Optional
    configInfo.DepthStencilInfo.maxDepthBounds = 1.0f;  // Optional
    configInfo.DepthStencilInfo.stencilTestEnable = false;
    configInfo.DepthStencilInfo.front = new();  // Optional
    configInfo.DepthStencilInfo.back = new();   // Optional

    configInfo.PipelineLayout = VkPipelineLayout.Null;
    configInfo.RenderPass = VkRenderPass.Null;
    configInfo.Subpass = 0;

    configInfo.DynamicStatesEnables = new VkDynamicState[] { VkDynamicState.Viewport, VkDynamicState.Scissor };

    fixed (VkDynamicState* pStates = configInfo.DynamicStatesEnables) {
      configInfo.DynamicStateInfo.sType = VkStructureType.PipelineDynamicStateCreateInfo;
      configInfo.DynamicStateInfo.pDynamicStates = pStates;
      configInfo.DynamicStateInfo.dynamicStateCount = (uint)configInfo.DynamicStatesEnables.Length;
      configInfo.DynamicStateInfo.flags = 0;
    }

    return configInfo;
  }

  public unsafe void Dispose() {
    vkDestroyShaderModule(_device.LogicalDevice, _vertexShaderModule, null);
    vkDestroyShaderModule(_device.LogicalDevice, _fragmentShaderModule, null);
    vkDestroyPipeline(_device.LogicalDevice, _graphicsPipeline);
  }
}