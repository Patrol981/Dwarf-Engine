using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.Engine.AbstractionLayer;
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

    _fontTexture = new VulkanTexture(_device, texWidth, texHeight, "im_gui_texture");
    _fontTexture.SetTextureData(fontData);

    VkDescriptorImageInfo fontDescriptor = VkUtils.DescriptorImageInfo(_fontTexture.GetSampler(), _fontTexture.GetImageView(), VkImageLayout.ShaderReadOnlyOptimal);
    _descriptorWriter = new VulkanDescriptorWriter(_systemSetLayout, _systemDescriptorPool);
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
    memAllocInfo.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, MemoryProperty.DeviceLocal);

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
    var stagingBuffer = new DwarfBuffer(
      _device,
      (ulong)uploadSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    _device.WaitDevice();
    Application.Instance.Mutex.WaitOne();
    stagingBuffer.Map((ulong)uploadSize);
    stagingBuffer.WriteToBuffer(fontData, (ulong)uploadSize);
    Application.Instance.Mutex.ReleaseMutex();
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

    Application.Instance.Mutex.WaitOne();
    _device.FlushCommandBuffer(copyCmd, copyQueue, true);
    Application.Instance.Mutex.ReleaseMutex();
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
    _descriptorWriter = new VulkanDescriptorWriter(_systemSetLayout, _systemDescriptorPool);
    _descriptorWriter.WriteImage(0, &fontDescriptor);
    _descriptorWriter.Build(out _systemDescriptorSet);

    // io.Fonts.SetTexID((IntPtr)_systemDescriptorSet.Handle);
  }
  public void CreateBuffers() {
    _vertexBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ImDrawVert>(),
      BufferUsage.VertexBuffer,
      MemoryProperty.HostVisible
    );

    _indexBuffer = new(
      _device,
      (ulong)Unsafe.SizeOf<ImDrawVert>(),
      BufferUsage.IndexBuffer,
      MemoryProperty.HostVisible
    );
  }

  public unsafe void BindShaderData(FrameInfo frameInfo) {
    // vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipeline);
    _systemPipeline.Bind(frameInfo.CommandBuffer);

    ImGuiIOPtr io = ImGui.GetIO();

    // var viewport = VkUtils.Viewport(io.DisplaySize.X, io.DisplaySize.Y, 0.0f, 1.0f);
    // vkCmdSetViewport(frameInfo.CommandBuffer, 0, 1, &viewport);
    _renderer.CommandList.SetViewport(frameInfo.CommandBuffer, 0, 0, io.DisplaySize.X, io.DisplaySize.Y, 0.0f, 1.0f);

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

  public unsafe void SetScissorRect(FrameInfo frameInfo, ImDrawCmdPtr pcmd, ImDrawDataPtr drawData) {
    var clipOff = drawData.DisplayPos;
    var clipScale = drawData.FramebufferScale;

    Vector4 clipRect;
    clipRect.X = (pcmd.ClipRect.X - clipOff.X) * clipScale.X;
    clipRect.Y = (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y;
    clipRect.Z = (pcmd.ClipRect.Z - clipOff.X) * clipScale.X;
    clipRect.W = (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y;

    if (clipRect.X < 0.0f)
      clipRect.X = 0.0f;
    if (clipRect.Y < 0.0f)
      clipRect.Y = 0.0f;

    VkRect2D scissorRect;
    scissorRect.offset.x = (int)clipRect.X;
    scissorRect.offset.y = (int)clipRect.Y;
    scissorRect.extent.width = (uint)(clipRect.Z - clipRect.X);
    scissorRect.extent.height = (uint)(clipRect.W - clipRect.Y);

    // scissorRect.offset.x = System.Math.Max((int)pcmd.ClipRect.X, 0);
    // scissorRect.offset.y = System.Math.Max((int)pcmd.ClipRect.Y, 0);
    // scissorRect.extent.width = (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X);
    // scissorRect.extent.height = (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y);
    vkCmdSetScissor(frameInfo.CommandBuffer, 0, 1, &scissorRect);
  }
}
