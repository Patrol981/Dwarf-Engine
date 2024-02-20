using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using DwarfEngine.Vulkan;

using ImGuiNET;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.UI;
public partial class ImGuiController {
  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    var pipelineInfo = new VkPipelineLayoutCreateInfo() {
      setLayoutCount = (uint)layouts.Length
    };
    fixed (VkDescriptorSetLayout* layoutsPtr = layouts) {
      pipelineInfo.pSetLayouts = layoutsPtr;
    }
    VkPushConstantRange pushConstantRange = VkUtils.PushConstantRange(
      VkShaderStageFlags.Vertex,
      (uint)Unsafe.SizeOf<ImGuiPushConstant>(),
      0
    );
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _systemPipelineLayout).CheckResult();
  }

  private unsafe void CreatePipeline(
    VkRenderPass renderPass,
    string vertexName,
    string fragmentName,
    PipelineProvider pipelineProvider
  ) {
    _systemPipeline?.Dispose();
    _pipelineConfigInfo ??= new ImGuiPipeline();
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _systemPipelineLayout;
    _systemPipeline = new Pipeline(_device, vertexName, fragmentName, pipelineConfig, pipelineProvider);
  }

  public unsafe void InitTexture() {
    var io = ImGui.GetIO();
    io.Fonts.GetTexDataAsRGBA32(out nint fontData, out int texWidth, out int texHeight, out int bytesPerPixel);
    var uploadSize = texWidth * texHeight * bytesPerPixel * sizeof(byte);

    _fontTexture = new Texture(_device, texWidth, texHeight, "im_gui_texture");
    _fontTexture.SetTextureData(fontData);

    VkDescriptorImageInfo fontDescriptor = VkUtils.DescriptorImageInfo(_fontTexture.GetSampler(), _fontTexture.GetImageView(), VkImageLayout.ShaderReadOnlyOptimal);
    _descriptorWriter = new DescriptorWriter(_systemSetLayout, _systemDescriptorPool);
    _descriptorWriter.WriteImage(0, &fontDescriptor);
    _descriptorWriter.Build(out _systemDescriptorSet);

    // io.Fonts.SetTexID((IntPtr)_systemDescriptorSet.Handle);
  }

  public unsafe void InitTexture(VkQueue copyQueue) {
    var io = ImGui.GetIO();
    io.Fonts.GetTexDataAsRGBA32(out nint fontData, out int texWidth, out int texHeight, out int bytesPerPixel);
    var uploadSize = texWidth * texHeight * bytesPerPixel * sizeof(byte);

    var imageInfo = new VkImageCreateInfo();
    imageInfo.imageType = VkImageType.Image2D;

    imageInfo.format = VkFormat.R8G8B8A8Unorm;
    imageInfo.extent.width = (uint)texWidth;
    imageInfo.extent.height = (uint)texHeight;
    imageInfo.extent.depth = 1;
    imageInfo.mipLevels = 1;
    imageInfo.arrayLayers = 1;
    imageInfo.samples = VkSampleCountFlags.Count1;
    imageInfo.tiling = VkImageTiling.Optimal;
    imageInfo.usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst;
    imageInfo.sharingMode = VkSharingMode.Exclusive;
    imageInfo.initialLayout = VkImageLayout.Undefined;

    vkCreateImage(_device.LogicalDevice, &imageInfo, null, out _fontImage).CheckResult();
    VkMemoryRequirements memReqs;
    vkGetImageMemoryRequirements(_device.LogicalDevice, _fontImage, &memReqs);
    VkMemoryAllocateInfo memAllocInfo = new();
    memAllocInfo.allocationSize = memReqs.size;
    memAllocInfo.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal);

    vkAllocateMemory(_device.LogicalDevice, &memAllocInfo, null, out _fontMemory).CheckResult();
    vkBindImageMemory(_device.LogicalDevice, _fontImage, _fontMemory, 0).CheckResult();

    // Image view
    VkImageViewCreateInfo viewInfo = new();
    viewInfo.image = _fontImage;
    viewInfo.viewType = VkImageViewType.Image2D;
    viewInfo.format = VkFormat.R8G8B8A8Unorm;
    viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
    viewInfo.subresourceRange.levelCount = 1;
    viewInfo.subresourceRange.layerCount = 1;

    vkCreateImageView(_device.LogicalDevice, &viewInfo, null, out _fontView);

    // staging buffers
    var stagingBuffer = new Vulkan.Buffer(
      _device,
      (ulong)uploadSize,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    _device.WaitDevice();
    stagingBuffer.Map((ulong)uploadSize);
    stagingBuffer.WriteToBuffer(fontData, (ulong)uploadSize);
    // stagingBuffer.WriteToBuffer(fontData);
    stagingBuffer.Unmap();

    // Copy buffer data to font image
    var copyCmd = _device.CreateCommandBuffer(VkCommandBufferLevel.Primary, true);

    // prepare for transfer
    VkUtils.SetImageLayout(
      copyCmd,
      _fontImage,
      VkImageAspectFlags.Color,
      VkImageLayout.Undefined,
      VkImageLayout.TransferDstOptimal,
      VkPipelineStageFlags.Host,
      VkPipelineStageFlags.Transfer
    );

    // copy
    VkBufferImageCopy bufferCopyRegion = new();
    bufferCopyRegion.imageSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
    bufferCopyRegion.imageSubresource.layerCount = 1;
    bufferCopyRegion.imageExtent.width = (uint)texWidth;
    bufferCopyRegion.imageExtent.height = (uint)texHeight;
    bufferCopyRegion.imageExtent.depth = 1;

    vkCmdCopyBufferToImage(
      copyCmd,
      stagingBuffer.GetBuffer(),
      _fontImage,
      VkImageLayout.TransferDstOptimal,
      1,
      &bufferCopyRegion
    );

    // prepare for shader read
    VkUtils.SetImageLayout(
      copyCmd,
      _fontImage,
      VkImageAspectFlags.Color,
      VkImageLayout.TransferDstOptimal,
      VkImageLayout.ShaderReadOnlyOptimal,
      VkPipelineStageFlags.Transfer,
      VkPipelineStageFlags.FragmentShader
    );

    _device.FlushCommandBuffer(copyCmd, copyQueue, true);
    stagingBuffer.Dispose();

    // font texture sampler
    VkSamplerCreateInfo samplerInfo = new();
    samplerInfo.magFilter = VkFilter.Linear;
    samplerInfo.minFilter = VkFilter.Linear;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Linear;
    samplerInfo.addressModeU = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeV = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeW = VkSamplerAddressMode.Repeat;
    samplerInfo.minLod = -1000;
    samplerInfo.maxLod = 1000;
    samplerInfo.maxAnisotropy = 1.0f;
    samplerInfo.borderColor = VkBorderColor.FloatOpaqueWhite;

    vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out _sampler).CheckResult();

    VkDescriptorImageInfo fontDescriptor = VkUtils.DescriptorImageInfo(_sampler, _fontView, VkImageLayout.ShaderReadOnlyOptimal);
    _descriptorWriter = new DescriptorWriter(_systemSetLayout, _systemDescriptorPool);
    _descriptorWriter.WriteImage(0, &fontDescriptor);
    _descriptorWriter.Build(out _systemDescriptorSet);

    // io.Fonts.SetTexID((IntPtr)_systemDescriptorSet.Handle);
  }

  /*
  public unsafe void InitResources(VkRenderPass renderPass, VkQueue copyQueue, string vertexName, string fragmentName) {
    var io = ImGui.GetIO();

    // Create font texture
    IntPtr fontData;
    int texWidth, texHeight;
    io.Fonts.GetTexDataAsRGBA32(out fontData, out texWidth, out texHeight);
    var uploadSize = texWidth * texHeight * 4;

    // Create target image for copy
    var imageInfo = new VkImageCreateInfo();
    imageInfo.imageType = VkImageType.Image2D;
    imageInfo.format = VkFormat.R8G8B8A8Unorm;
    imageInfo.extent.width = (uint)texWidth;
    imageInfo.extent.height = (uint)texHeight;
    imageInfo.extent.depth = 1;
    imageInfo.mipLevels = 1;
    imageInfo.arrayLayers = 1;
    imageInfo.samples = VkSampleCountFlags.Count1;
    imageInfo.tiling = VkImageTiling.Optimal;
    imageInfo.usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst;
    imageInfo.sharingMode = VkSharingMode.Exclusive;
    imageInfo.initialLayout = VkImageLayout.Undefined;

    vkCreateImage(_device.LogicalDevice, &imageInfo, null, out _fontImage).CheckResult();
    VkMemoryRequirements memReqs;
    vkGetImageMemoryRequirements(_device.LogicalDevice, _fontImage, &memReqs);
    VkMemoryAllocateInfo memAllocInfo = new();
    memAllocInfo.allocationSize = memReqs.size;
    memAllocInfo.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal);

    vkAllocateMemory(_device.LogicalDevice, &memAllocInfo, null, out _fontMemory).CheckResult();
    vkBindImageMemory(_device.LogicalDevice, _fontImage, _fontMemory, 0).CheckResult();

    // Image view
    VkImageViewCreateInfo viewInfo = new();
    viewInfo.image = _fontImage;
    viewInfo.viewType = VkImageViewType.Image2D;
    viewInfo.format = VkFormat.R8G8B8A8Unorm;
    viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
    viewInfo.subresourceRange.levelCount = 1;
    viewInfo.subresourceRange.layerCount = 1;

    vkCreateImageView(_device.LogicalDevice, &viewInfo, null, out _fontView);

    // staging buffers
    var stagingBuffer = new Vulkan.Buffer(
      _device,
      (ulong)uploadSize,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    _device.WaitDevice();
    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(fontData, (ulong)uploadSize);
    stagingBuffer.Unmap();

    // Copy buffer data to font image
    var copyCmd = _device.CreateCommandBuffer(VkCommandBufferLevel.Primary, true);

    // prepare for transfer
    VkUtils.SetImageLayout(
      copyCmd,
      _fontImage,
      VkImageAspectFlags.Color,
      VkImageLayout.Undefined,
      VkImageLayout.TransferDstOptimal,
      VkPipelineStageFlags.Host,
      VkPipelineStageFlags.Transfer
    );

    // copy
    VkBufferImageCopy bufferCopyRegion = new();
    bufferCopyRegion.imageSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
    bufferCopyRegion.imageSubresource.layerCount = 1;
    bufferCopyRegion.imageExtent.width = (uint)texWidth;
    bufferCopyRegion.imageExtent.height = (uint)texHeight;
    bufferCopyRegion.imageExtent.depth = 1;

    vkCmdCopyBufferToImage(
      copyCmd,
      stagingBuffer.GetBuffer(),
      _fontImage,
      VkImageLayout.TransferDstOptimal,
      1,
      &bufferCopyRegion
    );

    // prepare for shader read
    VkUtils.SetImageLayout(
      copyCmd,
      _fontImage,
      VkImageAspectFlags.Color,
      VkImageLayout.TransferDstOptimal,
      VkImageLayout.ShaderReadOnlyOptimal,
      VkPipelineStageFlags.Transfer,
      VkPipelineStageFlags.FragmentShader
    );

    _device.FlushCommandBuffer(copyCmd, copyQueue, true);
    stagingBuffer.Dispose();

    // font texture sampler
    VkSamplerCreateInfo samplerInfo = new();
    samplerInfo.magFilter = VkFilter.Linear;
    samplerInfo.minFilter = VkFilter.Linear;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Linear;
    samplerInfo.addressModeU = VkSamplerAddressMode.ClampToEdge;
    samplerInfo.addressModeV = VkSamplerAddressMode.ClampToEdge;
    samplerInfo.addressModeW = VkSamplerAddressMode.ClampToEdge;
    samplerInfo.borderColor = VkBorderColor.FloatOpaqueWhite;

    vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out _sampler).CheckResult();

    // descriptor pool
    VkDescriptorPoolSize[] poolSizes = new VkDescriptorPoolSize[1];
    poolSizes[0] = VkUtils.DescriptorPoolSize(VkDescriptorType.CombinedImageSampler, 1);
    var descriptorPoolInfo = VkUtils.DescriptorPoolCreateInfo(poolSizes, 2);
    vkCreateDescriptorPool(_device.LogicalDevice, &descriptorPoolInfo, null, out _descriptorPool).CheckResult();

    // descriptor set layout
    VkDescriptorSetLayoutBinding[] setLayoutBindings = new VkDescriptorSetLayoutBinding[1];
    setLayoutBindings[0] = VkUtils.DescriptorSetLayoutBinding(
      VkDescriptorType.CombinedImageSampler,
      VkShaderStageFlags.Fragment,
      0
    );
    VkDescriptorSetLayoutCreateInfo descriptorLayout = VkUtils.DescriptorSetLayoutCreateInfo(setLayoutBindings);
    vkCreateDescriptorSetLayout(_device.LogicalDevice, &descriptorLayout, null, out _descriptorSetLayout).CheckResult();

    // descriptor set
    VkDescriptorSetAllocateInfo allocInfo;
    fixed (VkDescriptorSet* descriptorSetPtr = &_descriptorSet)
    fixed (VkDescriptorSetLayout* setLayoutPtr = &_descriptorSetLayout) {
      allocInfo = VkUtils.DescriptorSetAllocateInfo(_descriptorPool, setLayoutPtr, 1);
      vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, descriptorSetPtr).CheckResult();
    }
    VkDescriptorImageInfo fontDescriptor = VkUtils.DescriptorImageInfo(_sampler, _fontView, VkImageLayout.ShaderReadOnlyOptimal);
    VkWriteDescriptorSet[] writeDescriptorSets = new VkWriteDescriptorSet[1];
    writeDescriptorSets[0] = VkUtils.WriteDescriptorSet(_descriptorSet, VkDescriptorType.CombinedImageSampler, 0, fontDescriptor);
    fixed (VkWriteDescriptorSet* pWrites = writeDescriptorSets) {
      vkUpdateDescriptorSets(_device.LogicalDevice, (uint)writeDescriptorSets.Length, pWrites, 0, null);
    }

    // pipeline cache
    VkPipelineCacheCreateInfo pipelineCacheCreateInfo = new();
    vkCreatePipelineCache(_device.LogicalDevice, &pipelineCacheCreateInfo, null, out _pipelineCache).CheckResult();

    // pipeline layout
    // Push constants for UI rendering parameters
    VkPushConstantRange pushConstantRange = VkUtils.PushConstantRange(
      VkShaderStageFlags.Vertex,
      (uint)Unsafe.SizeOf<ImGuiPushConstant>(),
      0
    );
    VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo;
    fixed (VkDescriptorSetLayout* setLayoutPtr = &_descriptorSetLayout) {
      pipelineLayoutCreateInfo = VkUtils.PipelineLayoutCreateInfo(setLayoutPtr, 1);
    }
    pipelineLayoutCreateInfo.pushConstantRangeCount = 1;
    pipelineLayoutCreateInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineLayoutCreateInfo, null, out _pipelineLayout).CheckResult();

    // setup graphics pipeline for ui rendering
    VkPipelineInputAssemblyStateCreateInfo inputAssemblyState = VkUtils.PipelineInputAssemblyStateCreateInfo(
      VkPrimitiveTopology.TriangleList,
      0,
      false
    );

    VkPipelineRasterizationStateCreateInfo rasterizationState = VkUtils.PipelineRasterizationStateCreateInfo(
      VkPolygonMode.Fill,
      VkCullModeFlags.None,
      _frontFace
    );

    // enable blending
    VkPipelineColorBlendAttachmentState blendAttachmentState = new();
    blendAttachmentState.blendEnable = false;
    blendAttachmentState.colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A;
    blendAttachmentState.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
    blendAttachmentState.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
    blendAttachmentState.colorBlendOp = VkBlendOp.Add;
    blendAttachmentState.srcAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
    blendAttachmentState.dstAlphaBlendFactor = VkBlendFactor.Zero;
    blendAttachmentState.alphaBlendOp = VkBlendOp.Add;

    VkPipelineColorBlendStateCreateInfo colorBlendState = VkUtils.PipelineColorBlendStateCreateInfo(1, &blendAttachmentState);
    VkPipelineDepthStencilStateCreateInfo depthStencilState = VkUtils.PipelineDepthStencilStateCreateInfo(false, false, VkCompareOp.LessOrEqual);
    VkPipelineViewportStateCreateInfo viewportState = VkUtils.PipelineViewportStateCreateInfo(1, 1, 0);
    VkPipelineMultisampleStateCreateInfo multisampleState = VkUtils.PipelineMultisampleStateCreateInfo(VkSampleCountFlags.Count1);

    VkDynamicState[] dynamicStates = [
      VkDynamicState.Viewport,
      VkDynamicState.Scissor
    ];

    VkPipelineDynamicStateCreateInfo dynamicState = VkUtils.PipelineDynamicStateCreateInfo(dynamicStates);
    VkPipelineShaderStageCreateInfo[] shaderStages = new VkPipelineShaderStageCreateInfo[2];
    VkGraphicsPipelineCreateInfo pipelineCreateInfo = VkUtils.PipelineCreateInfo(_pipelineLayout, renderPass);


    pipelineCreateInfo.pInputAssemblyState = &inputAssemblyState;
    pipelineCreateInfo.pRasterizationState = &rasterizationState;
    pipelineCreateInfo.pColorBlendState = &colorBlendState;
    pipelineCreateInfo.pMultisampleState = &multisampleState;
    pipelineCreateInfo.pViewportState = &viewportState;
    pipelineCreateInfo.pDepthStencilState = &depthStencilState;
    pipelineCreateInfo.pDynamicState = &dynamicState;
    pipelineCreateInfo.stageCount = (uint)shaderStages.Length;
    fixed (VkPipelineShaderStageCreateInfo* pStages = shaderStages) {
      pipelineCreateInfo.pStages = pStages;
    }

    // Vertex bindings an attributes based on ImGui vertex definition
    VkVertexInputBindingDescription[] vertexInputBindings = [
      VkUtils.VertexInputBindingDescription(0, (uint)Unsafe.SizeOf<ImDrawVert>(), VkVertexInputRate.Vertex)
    ];

    // R8G8B8A8Unorm
    VkVertexInputAttributeDescription[] vertexInputAttributes = [
      VkUtils.VertexInputAttributeDescription(0, 0, VkFormat.R32G32Sfloat, (uint)Marshal.OffsetOf<ImDrawVert>("pos")),
      VkUtils.VertexInputAttributeDescription(0, 1, VkFormat.R32G32Sfloat, (uint)Marshal.OffsetOf<ImDrawVert>("uv")),
      VkUtils.VertexInputAttributeDescription(0, 2, VkFormat.R8G8B8A8Unorm, (uint)Marshal.OffsetOf<ImDrawVert>("col"))
    ];
    VkPipelineVertexInputStateCreateInfo vertexInputState = new();
    fixed (VkVertexInputAttributeDescription* attribPtr = vertexInputAttributes)
    fixed (VkVertexInputBindingDescription* inputPtr = vertexInputBindings) {
      vertexInputState.vertexBindingDescriptionCount = (uint)vertexInputBindings.Length;
      vertexInputState.pVertexBindingDescriptions = inputPtr;
      vertexInputState.vertexAttributeDescriptionCount = (uint)vertexInputAttributes.Length;
      vertexInputState.pVertexAttributeDescriptions = attribPtr;
    }

    pipelineCreateInfo.pVertexInputState = &vertexInputState;

    var vertexPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders", $"{vertexName}.spv");
    var fragmentPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders", $"{fragmentName}.spv");
    var vertexCode = File.ReadAllBytes(vertexPath);
    var fragmentCode = File.ReadAllBytes(fragmentPath);

    CreateShaderModule(vertexCode, out _vertexModule);
    CreateShaderModule(fragmentCode, out _fragmentModule);
    VkString entryPoint = new("main");

    // vertex
    shaderStages[0] = new();
    shaderStages[0].stage = VkShaderStageFlags.Vertex;
    shaderStages[0].module = _vertexModule;
    shaderStages[0].pName = entryPoint;
    shaderStages[0].flags = 0;
    shaderStages[0].pNext = null;

    //fragment
    shaderStages[1] = new();
    shaderStages[1].stage = VkShaderStageFlags.Fragment;
    shaderStages[1].module = _fragmentModule;
    shaderStages[1].pName = entryPoint;
    shaderStages[1].flags = 0;
    shaderStages[1].pNext = null;

    VkPipeline graphicsPipeline = VkPipeline.Null;
    vkCreateGraphicsPipelines(_device.LogicalDevice, _pipelineCache, 1, &pipelineCreateInfo, null, &graphicsPipeline);
    _pipeline = graphicsPipeline;
  }
  */

  public void CreateBuffers() {
    _vertexBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ImDrawVert>(),
      VkBufferUsageFlags.VertexBuffer,
      VkMemoryPropertyFlags.HostVisible
    );

    _indexBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ImDrawVert>(),
      VkBufferUsageFlags.IndexBuffer,
      VkMemoryPropertyFlags.HostVisible
    );
  }

  public unsafe void BindShaderData(FrameInfo frameInfo) {
    // vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipeline);
    _systemPipeline.Bind(frameInfo.CommandBuffer);

    ImGuiIOPtr io = ImGui.GetIO();

    var viewport = VkUtils.Viewport(io.DisplaySize.X, io.DisplaySize.Y, 0.0f, 1.0f);
    vkCmdSetViewport(frameInfo.CommandBuffer, 0, 1, &viewport);

    Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(
        0f,
        io.DisplaySize.X,
        0.0f, // io.DisplaySize.Y,
        io.DisplaySize.Y, // 0.0f,
        0.0f,
        1.0f);

    var push = new ImGuiPushConstant {
      Projection = mvp
    };

    vkCmdPushConstants(
      frameInfo.CommandBuffer,
      _systemPipelineLayout,
      VkShaderStageFlags.Vertex,
      0,
      (uint)Unsafe.SizeOf<ImGuiPushConstant>(),
      &push
    );
  }

  public unsafe void BindTexture(FrameInfo frameInfo) {
    fixed (VkDescriptorSet* descPtr = &_systemDescriptorSet) {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _systemPipelineLayout,
        0,
        1,
        descPtr,
        0,
        null
      );
    }
  }

  public unsafe void SetScissorRect(FrameInfo frameInfo, ImDrawCmdPtr pcmd) {
    VkRect2D scissorRect;
    scissorRect.offset.x = System.Math.Max((int)pcmd.ClipRect.X, 0);
    scissorRect.offset.y = System.Math.Max((int)pcmd.ClipRect.Y, 0);
    scissorRect.extent.width = (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X);
    scissorRect.extent.height = (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y);
    vkCmdSetScissor(frameInfo.CommandBuffer, 0, 1, &scissorRect);
  }
}
