using Vortice.Vulkan;

namespace Dwarf.Vulkan;

public class PipelineConfigInfo {
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

  /// <summary>
  /// <c>PielineConfigInfo</c>.<c>GetConfigInfo()</c> returns default config info.
  /// This method is overridable, so there is no need to write all that stuff all over again if want to
  /// make small changes to the pipeline
  /// </summary>
  public unsafe virtual PipelineConfigInfo GetConfigInfo() {
    var configInfo = this;

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
    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.Back;
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
    fixed (VkPipelineColorBlendAttachmentState* colorBlendAttachment = &configInfo.ColorBlendAttachment) {
      configInfo.ColorBlendInfo.pAttachments = colorBlendAttachment;
    }
    // configInfo.ColorBlendInfo.pAttachments = &configInfo.ColorBlendAttachment;
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

    return this;
  }
}