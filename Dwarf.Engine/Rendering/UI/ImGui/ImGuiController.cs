using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Engine.Globals;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using ImGuiNET;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering.UI;
public partial class ImGuiController : IDisposable {
  private readonly Device _device;
  private readonly Renderer _renderer;

  private VkSampler _sampler;
  private Vulkan.Buffer _vertexBuffer;
  private Vulkan.Buffer _indexBuffer;
  private int _vertexCount;
  private int _indexCount;
  private VkDeviceMemory _fontMemory = VkDeviceMemory.Null;
  private VkImage _fontImage = VkImage.Null;
  private VkImageView _fontView = VkImageView.Null;
  private VkPipelineCache _pipelineCache;
  private VkPipelineLayout _pipelineLayout;
  private VkPipeline _pipeline;
  private VkDescriptorPool _descriptorPool;
  private VkDescriptorSetLayout _descriptorSetLayout;
  private VkDescriptorSet _descriptorSet;
  private VkPhysicalDeviceDriverProperties _driverProperties;
  private ImGuiStylePtr _vulkanStyle;
  private int _selectedStyle = 0;

  private VkShaderModule _vertexModule = VkShaderModule.Null;
  private VkShaderModule _fragmentModule = VkShaderModule.Null;

  private int _vertexBufferSize = 0;
  private int _indexBufferSize = 0;

  private bool _frameBegun = false;

  private int _width;
  private int _height;
  private System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;
  private VkFrontFace _frontFace = VkFrontFace.Clockwise;

  [StructLayout(LayoutKind.Explicit)]
  struct ImGuiPushConstant {
    [FieldOffset(0)] public Matrix4x4 Projection;
    // [FieldOffset(64)] public Matrix4x4 Transform;

    // [FieldOffset(0)] public Vector2 Scale;
    // [FieldOffset(8)] public Vector2 Translate;
  }

  public unsafe ImGuiController(Device device, Renderer renderer) {
    _device = device;
    _renderer = renderer;

    ImGui.CreateContext();
  }

  public void Init(int width, int height) {
    _width = width;
    _height = height;

    CreateBuffers();

    IntPtr context = ImGui.CreateContext();
    ImGui.SetCurrentContext(context);
    var io = ImGui.GetIO();
    io.Fonts.AddFontDefault();

    io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    io.DisplaySize = new(width, height);
    io.DisplayFramebufferScale = new(1.0f, 1.0f);

    InitResources(_renderer.GetSwapchainRenderPass(), _device.GraphicsQueue, "imgui_vertex", "imgui_fragment");
    SetPerFrameImGuiData(1f / 60f);

    ImGui.NewFrame();
    _frameBegun = true;

    WindowState.s_Window.OnResizedEventDispatcher += WindowResized;
  }

  private void WindowResized(object? sender, EventArgs e) {
    var windowExtent = WindowState.s_Window.Extent;
    _width = (int)windowExtent.width;
    _height = (int)windowExtent.height;
    Logger.Info($"[ImGUI] Window Resized ({_width}{_height})");
  }

  private void SetStyle(int index) {
    switch (index) {
      case 0:
        var style = ImGui.GetStyle();
        style = _vulkanStyle;
        break;
      case 1:
        ImGui.StyleColorsClassic();
        break;
      case 2:
        ImGui.StyleColorsDark();
        break;
      case 3:
        ImGui.StyleColorsLight();
        break;
    }
  }

  public void NewFrame() {
    ImGui.NewFrame();

    ImGui.End();

    ImGui.ShowDemoWindow();

    ImGui.Render();
    // UpdateImBuffers(ImGui.GetDrawData());
  }

  private void SetPerFrameImGuiData(float deltaSeconds) {
    ImGuiIOPtr io = ImGui.GetIO();
    io.DisplaySize = new System.Numerics.Vector2(
      _width / _scaleFactor.X,
      _height / _scaleFactor.Y);
    io.DisplayFramebufferScale = _scaleFactor;
    io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
  }

  public void Render(FrameInfo frameInfo) {
    if (_frameBegun) {
      _frameBegun = false;
      ImGui.Render();
      RenderImDrawData(ImGui.GetDrawData(), frameInfo);
    }
  }

  public void Update(float deltaSeconds) {
    if (_frameBegun) {
      ImGui.Render();
    }

    SetPerFrameImGuiData(deltaSeconds);
    UpdateImGuiInput();

    _frameBegun = true;
    ImGui.NewFrame();
  }

  public void UpdateImGuiInput() {
    ImGuiIOPtr io = ImGui.GetIO();

    // Logger.Info($"LEFT MB {MouseState.GetInstance().QuickStateMouseButtons.Left}");

    io.MouseDown[0] = MouseState.GetInstance().QuickStateMouseButtons.Left;
    io.MouseDown[1] = MouseState.GetInstance().QuickStateMouseButtons.Right;
    io.MouseDown[2] = MouseState.GetInstance().QuickStateMouseButtons.Middle;

    var screenPoint = new Vector2((int)MouseState.GetInstance().MousePosition.X, (int)MouseState.GetInstance().MousePosition.Y);
    io.MousePos = new System.Numerics.Vector2(screenPoint.X, screenPoint.Y);

    // Logger.Info($"MOUSE POS: {screenPoint}");

  }

  public unsafe void RenderImDrawData(ImDrawDataPtr drawData, FrameInfo frameInfo) {
    // RenderImDrawData_NET(drawData, frameInfo);
    RenderImDrawData_CPP(drawData, frameInfo);
  }

  public unsafe void UpdateBuffers_CPP(ImDrawDataPtr drawData) {
    var vertexBufferSize = drawData.TotalVtxCount * sizeof(ImDrawVert);
    var indexBufferSize = drawData.TotalIdxCount * sizeof(ushort);

    if ((vertexBufferSize == 0) || (indexBufferSize == 0)) {
      return;
    }

    if (_vertexBuffer.GetBuffer() == VkBuffer.Null || _vertexCount != drawData.TotalVtxCount) {
      _vertexBuffer?.Dispose();
      _vertexBuffer = new(
        _device,
        (ulong)vertexBufferSize,
        VkBufferUsageFlags.VertexBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
      );
      _vertexBuffer.Map((ulong)vertexBufferSize);
    }

    if (_indexBuffer.GetBuffer() == VkBuffer.Null || _indexCount < drawData.TotalIdxCount) {
      _indexBuffer?.Dispose();
      _indexBuffer = new(
        _device,
        (ulong)indexBufferSize,
        VkBufferUsageFlags.IndexBuffer,
        VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
      );
      _indexBuffer.Map((ulong)indexBufferSize);
    }

    var vtxDst = _vertexBuffer.GetMappedMemory();
    var idxDst = _indexBuffer.GetMappedMemory();

    for (int n = 0; n < drawData.CmdListsCount; n++) {
      var cmdList = drawData.CmdLists[n];

      VkUtils.MemCopy(vtxDst, cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size * sizeof(ImDrawVert));
      VkUtils.MemCopy(idxDst, cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size * sizeof(ushort));

      vtxDst += cmdList.VtxBuffer.Size;
      idxDst += cmdList.IdxBuffer.Size;
    }

    // _vertexBuffer.Flush();
    // _indexBuffer.Flush();
  }

  public unsafe void RenderImDrawData_CPP(ImDrawDataPtr drawData, FrameInfo frameInfo) {
    // update buffers

    UpdateBuffers_CPP(drawData);

    BindTexture(frameInfo);
    BindShaderData(frameInfo);

    int vertexOffset = 0;
    uint indexOffset = 0;

    if (drawData.CmdListsCount > 0) {
      ulong[] offsets = { 0 };
      VkBuffer[] vertexBuffers = [_vertexBuffer.GetBuffer()];

      fixed (VkBuffer* vertexPtr = vertexBuffers)
      fixed (ulong* offsetsPtr = offsets) {
        vkCmdBindVertexBuffers(frameInfo.CommandBuffer, 0, 1, vertexPtr, offsetsPtr);
      }
      vkCmdBindIndexBuffer(frameInfo.CommandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint16);

      for (int i = 0; i < drawData.CmdListsCount; i++) {
        var cmdList = drawData.CmdLists[i];
        for (int j = 0; j < cmdList.CmdBuffer.Size; j++) {
          var pcmd = cmdList.CmdBuffer[j];
          SetScissorRect(frameInfo, pcmd);
          vkCmdDrawIndexed(frameInfo.CommandBuffer, pcmd.ElemCount, 1, indexOffset, vertexOffset, 0);
          indexOffset += pcmd.ElemCount;
        }
        vertexOffset += cmdList.VtxBuffer.Size;
      }
    }

  }

  public unsafe void RenderImDrawData_NET(ImDrawDataPtr drawData, FrameInfo frameInfo) {
    uint vertexOffsetInVertices = 0;
    uint indexOffsetInElements = 0;

    if (drawData.CmdListsCount == 0) {
      return;
    }

    uint totalVBSize = (uint)(drawData.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>());
    if (totalVBSize > _vertexBuffer.GetBufferSize()) {
      var newSize = totalVBSize * 1.5f;
      _device.WaitDevice();
      _vertexBuffer?.Dispose();
      _vertexBuffer = new(
        _device,
        (ulong)newSize,
        VkBufferUsageFlags.VertexBuffer,
        VkMemoryPropertyFlags.HostVisible
      );
      _vertexBuffer.Map();

      Logger.Info($"Resized dear imgui vertex buffer to new size {newSize}");
    }

    uint totalIBSize = (uint)(drawData.TotalIdxCount * sizeof(ushort));
    if (totalIBSize > _indexBuffer.GetBufferSize()) {
      var newSize = totalIBSize * 1.5f;
      _device.WaitDevice();
      _indexBuffer?.Dispose();
      _indexBuffer = new(
        _device,
        (ulong)newSize,
        VkBufferUsageFlags.IndexBuffer,
        VkMemoryPropertyFlags.HostVisible
      );
      _indexBuffer.Map();

      Logger.Info($"Resized dear imgui vertex buffer to new size {newSize}");
    }

    for (int i = 0; i < drawData.CmdListsCount; i++) {
      ImDrawListPtr cmdList = drawData.CmdLists[i];

      _vertexBuffer.WriteToBuffer(
        cmdList.VtxBuffer.Data,
        vertexOffsetInVertices * (ulong)Unsafe.SizeOf<ImDrawVert>()
      );

      _indexBuffer.WriteToBuffer(
        cmdList.IdxBuffer.Data,
        indexOffsetInElements * sizeof(ushort)
      );

      vertexOffsetInVertices += (uint)cmdList.VtxBuffer.Size;
      indexOffsetInElements += (uint)cmdList.IdxBuffer.Size;
    }

    ImGuiIOPtr io = ImGui.GetIO();
    BindShaderData(frameInfo);
    drawData.ScaleClipRects(io.DisplayFramebufferScale);

    _vertexBuffer.Flush();
    _indexBuffer.Flush();

    // Render command lists
    int vtxOffset = 0;
    int idxOffset = 0;

    if (drawData.CmdListsCount < 1) return;
    ulong[] offsets = { 0 };
    VkBuffer[] vertexBuffers = [_vertexBuffer.GetBuffer()];

    fixed (VkBuffer* vertexPtr = vertexBuffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(frameInfo.CommandBuffer, 0, 1, vertexPtr, offsetsPtr);
    }
    vkCmdBindIndexBuffer(frameInfo.CommandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint16);

    var viewport = VkUtils.Viewport(io.DisplaySize.X, io.DisplaySize.Y, 0.1f, 1.0f);
    vkCmdSetViewport(frameInfo.CommandBuffer, 0, 1, &viewport);

    for (int n = 0; n < drawData.CmdListsCount; n++) {
      ImDrawListPtr cmdList = drawData.CmdLists[n];
      for (int cmdIndex = 0; cmdIndex < cmdList.CmdBuffer.Size; cmdIndex++) {
        ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmdIndex];
        if (pcmd.UserCallback != IntPtr.Zero) {
          throw new NotImplementedException();
        } else {
          BindTexture(frameInfo);

          SetScissorRect(frameInfo, pcmd);

          // indexCount = pcm.ElemCount
          // instanceCount = 1
          // indexStart = pcmd.IdxOffset + (uint)idx_offset
          // vertexOffset = (int)pcmd.VtxOffset + vtx_offset
          // instanceStart = 0

          // vkCmdDraw(frameInfo.CommandBuffer, (uint)vtxOffset, 1, 0, 0);


          vkCmdDrawIndexed(
            frameInfo.CommandBuffer,
            pcmd.ElemCount,
            1,
            pcmd.IdxOffset + (uint)idxOffset,
            (int)pcmd.VtxOffset + (int)vtxOffset,
            0
          );

        }
        // idxOffset += (int)pcmd.ElemCount;
      }
      vtxOffset += cmdList.VtxBuffer.Size;
      idxOffset += cmdList.IdxBuffer.Size;
    }

  }

  public unsafe void RenderImDrawData_Old(ImDrawDataPtr drawData, FrameInfo frameInfo) {
    // UpdateImBuffers(drawData);

    var io = ImGui.GetIO();

    fixed (VkDescriptorSet* descPtr = &_descriptorSet) {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelineLayout,
        0,
        1,
        descPtr,
        0,
        null
      );
    }

    drawData.ScaleClipRects(io.DisplayFramebufferScale);

    vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipeline);

    var viewport = VkUtils.Viewport(io.DisplaySize.X, io.DisplaySize.Y, 0.0f, 1.0f);
    vkCmdSetViewport(frameInfo.CommandBuffer, 0, 1, &viewport);

    Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(
               0f,
               io.DisplaySize.X,
               io.DisplaySize.Y,
               0.0f,
               -1.0f,
               1.0f);

    var imguiPush = new ImGuiPushConstant {
      // Scale = new(2.0f / io.DisplaySize.X, 2.0f / io.DisplaySize.Y),
      // Translate = new(-1.0f, -1.0f),
      // Projection = frameInfo.Camera.GetProjectionMatrix() * frameInfo.Camera.GetViewMatrix(),
      Projection = mvp
      // Transform = Matrix4x4.CreateTranslation(new(0, 0, 0))
    };

    vkCmdPushConstants(
      frameInfo.CommandBuffer,
      _pipelineLayout,
      VkShaderStageFlags.Vertex,
      0,
      (uint)Unsafe.SizeOf<ImGuiPushConstant>(),
      &imguiPush
    );

    for (int n = 0; n < drawData.CmdListsCount; n++) {
      ImDrawListPtr cmdList = drawData.CmdLists[n];
      for (int cmdIndex = 0; cmdIndex < cmdList.CmdBuffer.Size; cmdIndex++) {
        ImDrawCmdPtr pcmd = cmdList.CmdBuffer[cmdIndex];
        if (pcmd.UserCallback != IntPtr.Zero) {
          throw new NotImplementedException();
        } else {
          var clip = pcmd.ClipRect;
          VkRect2D scissorRect;
          scissorRect.offset.x = (int)clip.X;
          scissorRect.offset.y = _height - (int)clip.W;
          scissorRect.extent.width = (uint)(clip.Z - clip.X);
          scissorRect.extent.height = (uint)(clip.W - clip.Y);
          vkCmdSetScissor(frameInfo.CommandBuffer, scissorRect);

          ulong[] offsets = { (ulong)pcmd.VtxOffset * (ulong)Unsafe.SizeOf<ImDrawVert>() };
          VkBuffer[] buffers = [_vertexBuffer.GetBuffer()];

          fixed (ulong* offsetsPtr = offsets)
          fixed (VkBuffer* vertexPtr = buffers) {
            vkCmdBindVertexBuffers(frameInfo.CommandBuffer, 0, 1, vertexPtr, offsetsPtr);
          }

          // vkCmdBindIndexBuffer(frameInfo.CommandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint16);
          // vkCmdBindIndexBuffer(frameInfo.CommandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint16);

          if ((io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0) {
            // Logger.Error("Draw Indexed #1");
            Logger.Info($"Index Count: {_indexCount} Vertex Count: {_vertexCount}");

            vkCmdDraw(
              frameInfo.CommandBuffer,
              (uint)_vertexCount,
              1,
              0,
              0
            );

            /*
            vkCmdDrawIndexed(
              frameInfo.CommandBuffer,
              (uint)_indexCount,
              1,
              0,
              0,
              0
            );
            */
          } else {
            Logger.Info("Draw Indexed #2");
            vkCmdDrawIndexed(
              frameInfo.CommandBuffer,
              pcmd.ElemCount,
              1,
              pcmd.IdxOffset * sizeof(ushort),
              (int)pcmd.VtxOffset * Unsafe.SizeOf<ImDrawVert>(),
              0
            );
          }
        }
      }
    }
  }

  private unsafe void UpdateImBuffers_Old(ImDrawDataPtr drawData) {
    if (drawData.CmdListsCount == 0) {
      return;
    }

    for (int i = 0; i < drawData.CmdListsCount; i++) {
      ImDrawListPtr cmdList = drawData.CmdLists[i];

      var vertexSize = cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
      if (_vertexBuffer == null || (ulong)vertexSize > _vertexBuffer.GetBufferSize()) {
        int newSize = (int)MathF.Max(_vertexBufferSize * 1.5f, vertexSize);
        _device.WaitDevice();
        _vertexBuffer?.Dispose();
        _vertexBuffer = new(
          _device,
          (ulong)newSize,
          VkBufferUsageFlags.VertexBuffer,
          VkMemoryPropertyFlags.HostVisible
        );
        _vertexBuffer.Map();
        _vertexCount = drawData.TotalVtxCount;
        _vertexBufferSize = newSize;
        Logger.Info($"Resized dear imgui vertex buffer to new size {_vertexBufferSize}");

        _vertexBuffer.Flush();
      }

      int indexSize = cmdList.IdxBuffer.Size * sizeof(ushort);
      if (_indexBuffer == null || (ulong)indexSize > _indexBuffer.GetBufferSize()) {
        int newSize = (int)MathF.Max(_indexBufferSize * 1.5f, indexSize);
        _device.WaitDevice();
        _indexBuffer?.Dispose();
        _indexBuffer = new(
          _device,
          (ulong)newSize,
          VkBufferUsageFlags.IndexBuffer,
          VkMemoryPropertyFlags.HostVisible
        );
        _indexBuffer.Map();
        _indexCount = drawData.TotalIdxCount;
        _indexBufferSize = newSize;
        Logger.Info($"Resized dear imgui index buffer to new size {_indexBufferSize}");

        _indexBuffer.Flush();
      }
    }
  }

  public unsafe void UpdateBuffers_Old() {
    var imDrawData = ImGui.GetDrawData();

    var indexTypeSize = sizeof(int);

    // Note: Alignment is done inside buffer creation
    var vertexBuffSize = imDrawData.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>();
    var indexBuffSize = imDrawData.TotalIdxCount * indexTypeSize;

    if ((vertexBuffSize == 0) || (indexBuffSize == 0)) {
      return;
    }

    // Update buffers only if vertex or index count has been changed compared to current buffer size

    // Vertex buffer
    if ((_vertexBuffer == null) || (_vertexCount != imDrawData.TotalVtxCount)) {
      _device.WaitDevice();
      _vertexBuffer?.Dispose();
      _vertexBuffer = new(
        _device,
        (ulong)vertexBuffSize,
        VkBufferUsageFlags.VertexBuffer,
        VkMemoryPropertyFlags.HostVisible
      );
      _vertexCount = ImGui.GetDrawData().TotalVtxCount;
      _vertexBuffer.Map();
      Logger.Info($"Resized dear imgui vertex buffer to new size {vertexBuffSize}");
    }

    // Index buffer
    if ((_indexBuffer == null) || (_indexCount != imDrawData.TotalIdxCount)) {
      _device.WaitDevice();
      _indexBuffer?.Dispose();
      _indexBuffer = new(
        _device,
        (ulong)indexBuffSize,
        VkBufferUsageFlags.IndexBuffer,
        VkMemoryPropertyFlags.HostVisible
      );
      _indexCount = imDrawData.TotalIdxCount;
      _indexBuffer.Map();
    }

    // Upload data
    var vtxDst = _vertexBuffer.GetMappedMemory();
    // var idxDst = _indexBuffer.GetMappedMemory();

    for (int n = 0; n < ImGui.GetDrawData().CmdListsCount; n++) {
      ImDrawListPtr drawListPtr = ImGui.GetDrawData().CmdLists[n];
      // ImDrawList drawList = drawListPtr;

      VkUtils.MemCopy(vtxDst, drawListPtr.VtxBuffer.Data, drawListPtr.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>());
      // VkUtils.MemCopy(_indexBuffer.GetMappedMemory(), drawListPtr.IdxBuffer.Data, drawListPtr.IdxBuffer.Size * indexTypeSize);

      vtxDst += drawListPtr.VtxBuffer.Size;
      // idxDst += drawListPtr.IdxBuffer.Size;
      // _vertexBuffer.AddToMapped(drawListPtr.VtxBuffer.Size);
      //  _indexBuffer.AddToMapped(drawListPtr.IdxBuffer.Size);
    }

    /*
    for (int n = 0; n < imDrawData->CmdListsCount; n++) {
      var cmdList = imDrawData->CmdLists.Address<ImDrawList>(n);
      var cmdListData = imDrawData->CmdLists.Data;
      var test = (ImDrawList*)cmdList;
      VkUtils.MemCopy(vtxDst, test->VtxBuffer.Data, test->VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>());
      VkUtils.MemCopy(idxDst, test->IdxBuffer.Data, test->IdxBuffer.Size * sizeof(uint));

      vtxDst += test->VtxBuffer.Size;
      idxDst += test->IdxBuffer.Size;
    }
    */

    // Flush to make writes visible to GPU
    _vertexBuffer.Flush();
    // _indexBuffer.Flush();
  }

  public unsafe void DrawFrame(VkCommandBuffer commandBuffer) {
    var io = ImGui.GetIO();

    fixed (VkDescriptorSet* descPtr = &_descriptorSet) {
      vkCmdBindDescriptorSets(
        commandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelineLayout,
        0,
        1,
        descPtr,
        0,
        null
      );
    }

    vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _pipeline);

    var viewport = VkUtils.Viewport(io.DisplaySize.X, io.DisplaySize.Y, 0.0f, 1.0f);
    vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

    // UI scale and translate via push constants
    var imguiPush = new ImGuiPushConstant() {
      // Scale = new(2.0f / io.DisplaySize.X, 2.0f / io.DisplaySize.Y),
      // Translate = new(-1.0f, -1.0f),
      // Scale = new(1, 1)
    };
    vkCmdPushConstants(
      commandBuffer,
      _pipelineLayout,
      VkShaderStageFlags.Vertex,
      0,
      (uint)Unsafe.SizeOf<ImGuiPushConstant>(),
      &imguiPush
    );

    // Render commands
    var imDrawData = ImGui.GetDrawData();
    int vertexOffset = 0;

    if (imDrawData.CmdListsCount > 0) {
      ulong[] offsets = { 0 };
      VkBuffer[] buffers = [_vertexBuffer.GetBuffer()];

      fixed (ulong* offsetsPtr = offsets)
      fixed (VkBuffer* vertexPtr = buffers) {
        vkCmdBindVertexBuffers(commandBuffer, 0, 1, vertexPtr, offsetsPtr);
      }

      // vkCmdBindIndexBuffer(commandBuffer, _indexBuffer.GetBuffer(), 0, VkIndexType.Uint16);

      for (int i = 0; i < ImGui.GetDrawData().CmdListsCount; i++) {
        ImDrawListPtr cmdListPtr = ImGui.GetDrawData().CmdLists[i];
        for (int j = 0; j < cmdListPtr.CmdBuffer.Size; j++) {
          ImDrawCmdPtr drawCmdPtr = cmdListPtr.CmdBuffer[j];
          // var pCmd = cmdList.CmdBuffer.Ref<ImDrawCmd>(j);
          VkRect2D scissorRect;
          scissorRect.offset.x = System.Math.Max((int)(drawCmdPtr.ClipRect.X), 0);
          scissorRect.offset.y = System.Math.Max((int)(drawCmdPtr.ClipRect.Y), 0);
          scissorRect.extent.width = (uint)(drawCmdPtr.ClipRect.Z - drawCmdPtr.ClipRect.X);
          scissorRect.extent.height = (uint)(drawCmdPtr.ClipRect.W - drawCmdPtr.ClipRect.Y);
          vkCmdSetScissor(commandBuffer, 0, 1, &scissorRect);
          // vkCmdDrawIndexed(commandBuffer, drawCmdPtr.ElemCount, 1, indexOffset, vertexOffset, 0);
          Logger.Info("Drawing");
          vkCmdDraw(commandBuffer, (uint)_vertexCount, 1, 0, 0);
          // indexOffset += drawCmdPtr.ElemCount;
        }
        vertexOffset += cmdListPtr.VtxBuffer.Size;
      }
    }
  }

  private unsafe void CreateShaderModule(byte[] data, out VkShaderModule module) {
    vkCreateShaderModule(_device.LogicalDevice, data, null, out module).CheckResult();
  }

  public unsafe void Dispose() {
    ImGui.DestroyContext();
    _vertexBuffer?.Dispose();
    _indexBuffer?.Dispose();
    vkDestroyShaderModule(_device.LogicalDevice, _vertexModule, null);
    vkDestroyShaderModule(_device.LogicalDevice, _fragmentModule, null);
    vkDestroyImage(_device.LogicalDevice, _fontImage, null);
    vkDestroyImageView(_device.LogicalDevice, _fontView, null);
    vkFreeMemory(_device.LogicalDevice, _fontMemory, null);
    vkDestroySampler(_device.LogicalDevice, _sampler, null);
    vkDestroyPipelineCache(_device.LogicalDevice, _pipelineCache, null);
    vkDestroyPipeline(_device.LogicalDevice, _pipeline, null);
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout, null);
    vkDestroyDescriptorPool(_device.LogicalDevice, _descriptorPool, null);
    vkDestroyDescriptorSetLayout(_device.LogicalDevice, _descriptorSetLayout, null);
  }
}
