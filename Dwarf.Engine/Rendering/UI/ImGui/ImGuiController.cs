using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.Globals;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;
using Dwarf.Vulkan;

using ImGuiNET;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.UI;
public partial class ImGuiController : IDisposable {
  private readonly VulkanDevice _device;
  private readonly Renderer _renderer;

  private DwarfBuffer _vertexBuffer = default!;
  private DwarfBuffer _indexBuffer = default!;
  private int _vertexCount;
  private int _indexCount;

  // custom
  private VkSampler _sampler = VkSampler.Null;
  private VkDeviceMemory _fontMemory = VkDeviceMemory.Null;
  private VkImage _fontImage = VkImage.Null;
  private VkImageView _fontView = VkImageView.Null;
  private VkPipelineCache _pipelineCache = VkPipelineCache.Null;

  // system based
  protected PipelineConfigInfo _pipelineConfigInfo = default!;
  protected VkPipelineLayout _systemPipelineLayout = VkPipelineLayout.Null;
  protected Pipeline _systemPipeline = null!;
  protected DescriptorPool _systemDescriptorPool = null!;
  protected DescriptorSetLayout _systemSetLayout = null!;
  protected VkDescriptorSet _systemDescriptorSet = VkDescriptorSet.Null;
  protected VulkanDescriptorWriter _descriptorWriter = null!;

  private VulkanTexture _fontTexture = default!;

  private bool _frameBegun = false;

  private int _width;
  private int _height;
  private System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;

  // private readonly Keys[] _allKeys = Enum.GetValues<Keys>();
  // private readonly List<char> _pressedChars = new List<char>();

  private readonly IntPtr _fontAtlasId = -1;

  [StructLayout(LayoutKind.Explicit)]
  struct ImGuiPushConstant {
    [FieldOffset(0)] public Matrix4x4 Projection;
  }

  public unsafe ImGuiController(VulkanDevice device, Renderer renderer) {
    _device = device;
    _renderer = renderer;

    ImGui.CreateContext();
  }

  public unsafe Task InitResources() {
    var descriptorCount = (uint)_renderer.MAX_FRAMES_IN_FLIGHT * 2;

    _systemDescriptorPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(1000)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 1000)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.None)
      .Build();

    _systemSetLayout = new DescriptorSetLayout.Builder(_device)
      .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.All)
      .Build();

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      _systemSetLayout.GetDescriptorSetLayout()
    ];

    InitTexture(_device.GraphicsQueue);

    VkPipelineCacheCreateInfo pipelineCacheCreateInfo = new();
    vkCreatePipelineCache(_device.LogicalDevice, &pipelineCacheCreateInfo, null, out _pipelineCache).CheckResult();

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(_renderer.GetSwapchainRenderPass(), "imgui_vertex", "imgui_fragment", new PipelineImGuiProvider());

    return Task.CompletedTask;
  }

  public async Task<Task> Init(int width, int height) {
    _width = width;
    _height = height;

    CreateBuffers();

    IntPtr context = ImGui.CreateContext();
    ImGui.SetCurrentContext(context);

    var io = ImGui.GetIO();
    // var font = io.Fonts.AddFontDefault();
    io.Fonts.SetTexID(_fontAtlasId);

    io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    io.DisplaySize = new(width, height);
    io.DisplayFramebufferScale = new(1.0f, 1.0f);

    // InitResources(_renderer.GetSwapchainRenderPass(), _device.GraphicsQueue, "imgui_vertex", "imgui_fragment");
    await InitResources();

    SetPerFrameImGuiData(1f / 60f);
    CreateStyles();

    ImGui.NewFrame();
    _frameBegun = true;

    WindowState.s_Window.OnResizedEventDispatcher += WindowResized;

    return Task.CompletedTask;
  }

  private bool TryMapKey(Keys key, out ImGuiKey keyResult) {
    ImGuiKey KeyToImGuiKeyShortcut(Keys keyToConvert, Keys startKey1, ImGuiKey startKey2) {
      int changeFromStart1 = (int)keyToConvert - (int)startKey1;
      return startKey2 + changeFromStart1;
    }

    keyResult = key switch {
      >= Keys.GLFW_KEY_F1 and <= Keys.GLFW_KEY_F24 => KeyToImGuiKeyShortcut(key, Keys.GLFW_KEY_F1, ImGuiKey.F1),
      >= Keys.GLFW_KEY_A and <= Keys.GLFW_KEY_Z => KeyToImGuiKeyShortcut(key, Keys.GLFW_KEY_A, ImGuiKey.A),
      >= Keys.GLFW_KEY_0 and <= Keys.GLFW_KEY_9 => KeyToImGuiKeyShortcut(key, Keys.GLFW_KEY_0, ImGuiKey._0),
      Keys.GLFW_KEY_LEFT_SHIFT or Keys.GLFW_KEY_RIGHT_SHIFT => ImGuiKey.ModShift,
      Keys.GLFW_KEY_LEFT_CONTROL or Keys.GLFW_KEY_RIGHT_CONTROL => ImGuiKey.ModCtrl,
      Keys.GLFW_KEY_LEFT_ALT or Keys.GLFW_KEY_RIGHT_ALT => ImGuiKey.ModAlt,
      Keys.GLFW_KEY_LEFT_SUPER or Keys.GLFW_KEY_RIGHT_SUPER => ImGuiKey.ModSuper,
      Keys.GLFW_KEY_MENU => ImGuiKey.Menu,
      Keys.GLFW_KEY_UP => ImGuiKey.UpArrow,
      Keys.GLFW_KEY_DOWN => ImGuiKey.DownArrow,
      Keys.GLFW_KEY_LEFT => ImGuiKey.LeftArrow,
      Keys.GLFW_KEY_RIGHT => ImGuiKey.RightArrow,
      Keys.GLFW_KEY_ENTER => ImGuiKey.Enter,
      Keys.GLFW_KEY_ESCAPE => ImGuiKey.Escape,
      Keys.GLFW_KEY_SPACE => ImGuiKey.Space,
      Keys.GLFW_KEY_TAB => ImGuiKey.Tab,
      Keys.GLFW_KEY_BACKSPACE => ImGuiKey.Backspace,
      Keys.GLFW_KEY_INSERT => ImGuiKey.Insert,
      Keys.GLFW_KEY_DELETE => ImGuiKey.Delete,
      Keys.GLFW_KEY_PAGE_UP => ImGuiKey.PageUp,
      Keys.GLFW_KEY_PAGE_DOWN => ImGuiKey.PageDown,
      Keys.GLFW_KEY_HOME => ImGuiKey.Home,
      Keys.GLFW_KEY_END => ImGuiKey.End,
      Keys.GLFW_KEY_CAPS_LOCK => ImGuiKey.CapsLock,
      Keys.GLFW_KEY_SCROLL_LOCK => ImGuiKey.ScrollLock,
      Keys.GLFW_KEY_PRINT_SCREEN => ImGuiKey.PrintScreen,
      Keys.GLFW_KEY_PAUSE => ImGuiKey.Pause,
      Keys.GLFW_KEY_NUM_LOCK => ImGuiKey.NumLock,
      Keys.GLFW_KEY_GRAVE_ACCENT => ImGuiKey.GraveAccent,
      Keys.GLFW_KEY_MINUS => ImGuiKey.Minus,
      Keys.GLFW_KEY_EQUAL => ImGuiKey.Equal,
      Keys.GLFW_KEY_LEFT_BRACKET => ImGuiKey.LeftBracket,
      Keys.GLFW_KEY_RIGHT_BRACKET => ImGuiKey.RightBracket,
      Keys.GLFW_KEY_SEMICOLON => ImGuiKey.Semicolon,
      Keys.GLFW_KEY_APOSTROPHE => ImGuiKey.Apostrophe,
      Keys.GLFW_KEY_COMMA => ImGuiKey.Comma,
      Keys.GLFW_KEY_PERIOD => ImGuiKey.Period,
      Keys.GLFW_KEY_SLASH => ImGuiKey.Slash,
      Keys.GLFW_KEY_BACKSLASH => ImGuiKey.Backslash,
      _ => ImGuiKey.None
    };

    return keyResult != ImGuiKey.None;
  }

  private void CreateStyles() {
    var colors = ImGui.GetStyle().Colors;
    colors[(int)ImGuiCol.Text] = new(1.00f, 1.00f, 1.00f, 1.00f);
    colors[(int)ImGuiCol.TextDisabled] = new(0.50f, 0.50f, 0.50f, 1.00f);
    colors[(int)ImGuiCol.WindowBg] = new(0.10f, 0.10f, 0.10f, 1.00f);
    colors[(int)ImGuiCol.ChildBg] = new(0.00f, 0.00f, 0.00f, 0.00f);
    colors[(int)ImGuiCol.PopupBg] = new(0.19f, 0.19f, 0.19f, 0.92f);
    colors[(int)ImGuiCol.Border] = new(0.19f, 0.19f, 0.19f, 0.29f);
    colors[(int)ImGuiCol.BorderShadow] = new(0.00f, 0.00f, 0.00f, 0.24f);
    colors[(int)ImGuiCol.FrameBg] = new(0.05f, 0.05f, 0.05f, 0.54f);
    colors[(int)ImGuiCol.FrameBgHovered] = new(0.19f, 0.19f, 0.19f, 0.54f);
    colors[(int)ImGuiCol.FrameBgActive] = new(0.20f, 0.22f, 0.23f, 1.00f);
    colors[(int)ImGuiCol.TitleBg] = new(0.00f, 0.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.TitleBgActive] = new(0.06f, 0.06f, 0.06f, 1.00f);
    colors[(int)ImGuiCol.TitleBgCollapsed] = new(0.00f, 0.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.MenuBarBg] = new(0.14f, 0.14f, 0.14f, 1.00f);
    colors[(int)ImGuiCol.ScrollbarBg] = new(0.05f, 0.05f, 0.05f, 0.54f);
    colors[(int)ImGuiCol.ScrollbarGrab] = new(0.34f, 0.34f, 0.34f, 0.54f);
    colors[(int)ImGuiCol.ScrollbarGrabHovered] = new(0.40f, 0.40f, 0.40f, 0.54f);
    colors[(int)ImGuiCol.ScrollbarGrabActive] = new(0.56f, 0.56f, 0.56f, 0.54f);
    colors[(int)ImGuiCol.CheckMark] = new(0.33f, 0.67f, 0.86f, 1.00f);
    colors[(int)ImGuiCol.SliderGrab] = new(0.34f, 0.34f, 0.34f, 0.54f);
    colors[(int)ImGuiCol.SliderGrabActive] = new(0.56f, 0.56f, 0.56f, 0.54f);
    colors[(int)ImGuiCol.Button] = new(0.859f, 0.369f, 0.231f, 1f);
    colors[(int)ImGuiCol.ButtonHovered] = new(0.19f, 0.19f, 0.19f, 1f);
    colors[(int)ImGuiCol.ButtonActive] = new(0.20f, 0.22f, 0.23f, 1.00f);
    colors[(int)ImGuiCol.Header] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.HeaderHovered] = new(0.00f, 0.00f, 0.00f, 0.36f);
    colors[(int)ImGuiCol.HeaderActive] = new(0.20f, 0.22f, 0.23f, 0.33f);
    colors[(int)ImGuiCol.Separator] = new(0.28f, 0.28f, 0.28f, 0.29f);
    colors[(int)ImGuiCol.SeparatorHovered] = new(0.44f, 0.44f, 0.44f, 0.29f);
    colors[(int)ImGuiCol.SeparatorActive] = new(0.40f, 0.44f, 0.47f, 1.00f);
    colors[(int)ImGuiCol.ResizeGrip] = new(0.28f, 0.28f, 0.28f, 0.29f);
    colors[(int)ImGuiCol.ResizeGripHovered] = new(0.44f, 0.44f, 0.44f, 0.29f);
    colors[(int)ImGuiCol.ResizeGripActive] = new(0.40f, 0.44f, 0.47f, 1.00f);
    colors[(int)ImGuiCol.Tab] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TabHovered] = new(0.14f, 0.14f, 0.14f, 1.00f);
    colors[(int)ImGuiCol.TabActive] = new(0.20f, 0.20f, 0.20f, 0.36f);
    colors[(int)ImGuiCol.TabUnfocused] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TabUnfocusedActive] = new(0.14f, 0.14f, 0.14f, 1.00f);
    colors[(int)ImGuiCol.DockingPreview] = new(0.33f, 0.67f, 0.86f, 1.00f);
    colors[(int)ImGuiCol.DockingEmptyBg] = new(1.00f, 0.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotLines] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotLinesHovered] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotHistogram] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.PlotHistogramHovered] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.TableHeaderBg] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TableBorderStrong] = new(0.00f, 0.00f, 0.00f, 0.52f);
    colors[(int)ImGuiCol.TableBorderLight] = new(0.28f, 0.28f, 0.28f, 0.29f);
    colors[(int)ImGuiCol.TableRowBg] = new(0.00f, 0.00f, 0.00f, 0.00f);
    colors[(int)ImGuiCol.TableRowBgAlt] = new(1.00f, 1.00f, 1.00f, 0.06f);
    colors[(int)ImGuiCol.TextSelectedBg] = new(0.20f, 0.22f, 0.23f, 1.00f);
    colors[(int)ImGuiCol.DragDropTarget] = new(0.33f, 0.67f, 0.86f, 1.00f);
    colors[(int)ImGuiCol.NavHighlight] = new(1.00f, 1.00f, 0.00f, 1.00f);
    colors[(int)ImGuiCol.NavWindowingHighlight] = new(1.00f, 1.00f, 0.00f, 0.70f);
    colors[(int)ImGuiCol.NavWindowingDimBg] = new(1.00f, 1.00f, 0.00f, 0.20f);
    colors[(int)ImGuiCol.ModalWindowDimBg] = new(1.00f, 1.00f, 0.00f, 0.35f);

    var style = ImGui.GetStyle();
    style.WindowPadding = new(8.00f, 8.00f);
    style.FramePadding = new(5.00f, 2.00f);
    style.CellPadding = new(6.00f, 6.00f);
    style.ItemSpacing = new(6.00f, 6.00f);
    style.ItemInnerSpacing = new(6.00f, 6.00f);
    style.TouchExtraPadding = new(0.00f, 0.00f);
    style.IndentSpacing = 25;
    style.ScrollbarSize = 15;
    style.GrabMinSize = 10;
    style.WindowBorderSize = 1;
    style.ChildBorderSize = 1;
    style.PopupBorderSize = 1;
    style.FrameBorderSize = 1;
    style.TabBorderSize = 1;
    style.WindowRounding = 0;
    style.ChildRounding = 4;
    style.FrameRounding = 3;
    style.PopupRounding = 4;
    style.ScrollbarRounding = 9;
    style.GrabRounding = 3;
    style.LogSliderDeadzone = 4;
    style.TabRounding = 4;
  }

  private void WindowResized(object? sender, EventArgs e) {
    var windowExtent = WindowState.s_Window.Extent;
    _width = (int)windowExtent.Width;
    _height = (int)windowExtent.Height;
    Logger.Info($"[ImGUI] Window Resized ({_width}{_height})");
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
    io.MouseDown[0] = MouseState.GetInstance().QuickStateMouseButtons.Left;
    io.MouseDown[1] = MouseState.GetInstance().QuickStateMouseButtons.Right;
    io.MouseDown[2] = MouseState.GetInstance().QuickStateMouseButtons.Middle;
    var screenPoint = new Vector2((int)MouseState.GetInstance().MousePosition.X, (int)MouseState.GetInstance().MousePosition.Y);
    io.MousePos = new System.Numerics.Vector2(screenPoint.X, screenPoint.Y);

    // _pressedChars.Clear();

    for (int key = 45; key < 90; key++) {
      // foreach (int key in Enum.GetValues(typeof(Keys))) {
      if (key == (int)Keys.GLFW_KEY_UNKNOWN) {
        continue;
      }

      // io.InputQueueCharacters.

      if (KeyboardState.Instance.KeyStates.TryGetValue(key, out var state)) {
        if (Input.GetKeyDown((Keys)key)) {
          io.AddInputCharacter((char)key);
        }
      }


      // io.AddInputCharacter((char)key);
      // io.KeysData[key].Down = Convert.ToByte(Input.GetKeyDown((Keys)key));

      if (TryMapKey((Keys)key, out var imKey)) {
        // io.AddKeyEvent(imKey, true);
        io.AddKeyEvent(imKey, Input.GetKey((Keys)key));
        if (Input.GetKeyDown(Keys.GLFW_KEY_BACKSPACE)) {
          Logger.Info("ayo");

        }
      }
    }
  }

  public unsafe void UpdateBuffers(ImDrawDataPtr drawData) {
    var vertexBufferSize = drawData.TotalVtxCount * sizeof(ImDrawVert);
    var indexBufferSize = drawData.TotalIdxCount * sizeof(ushort);

    if ((vertexBufferSize == 0) || (indexBufferSize == 0)) {
      return;
    }

    if ((_vertexBuffer.GetBuffer() == VkBuffer.Null) || (_vertexCount < drawData.TotalVtxCount)) {
      _vertexCount = drawData.TotalVtxCount;

      _vertexBuffer?.Dispose();
      _vertexBuffer = new(
        _device,
        (ulong)sizeof(ImDrawVert),
        (ulong)drawData.TotalVtxCount,
        BufferUsage.VertexBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
    }

    if ((_indexBuffer.GetBuffer() == VkBuffer.Null) || (_indexCount < drawData.TotalIdxCount)) {
      _indexCount = drawData.TotalIdxCount;

      _indexBuffer?.Dispose();
      _indexBuffer = new(
        _device,
        (ulong)sizeof(ushort),
        (ulong)drawData.TotalIdxCount,
        BufferUsage.IndexBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
    }

    ImDrawVert* vtxDst = null;
    ushort* idxDst = null;

    vkMapMemory(_device.LogicalDevice, _vertexBuffer.GetVkDeviceMemory(), 0, _vertexBuffer.GetBufferSize(), 0, (void**)&vtxDst);
    vkMapMemory(_device.LogicalDevice, _indexBuffer.GetVkDeviceMemory(), 0, _indexBuffer.GetBufferSize(), 0, (void**)&idxDst);

    for (int n = 0; n < drawData.CmdListsCount; n++) {
      var cmdList = drawData.CmdLists[n];

      Unsafe.CopyBlock(vtxDst, cmdList.VtxBuffer.Data.ToPointer(), (uint)cmdList.VtxBuffer.Size * (uint)sizeof(ImDrawVert));
      Unsafe.CopyBlock(idxDst, cmdList.IdxBuffer.Data.ToPointer(), (uint)cmdList.IdxBuffer.Size * sizeof(ushort));

      vtxDst += cmdList.VtxBuffer.Size;
      idxDst += cmdList.IdxBuffer.Size;
    }

    vkUnmapMemory(_device.LogicalDevice, _vertexBuffer.GetVkDeviceMemory());
    vkUnmapMemory(_device.LogicalDevice, _indexBuffer.GetVkDeviceMemory());
  }

  public unsafe void RenderImDrawData(ImDrawDataPtr drawData, FrameInfo frameInfo) {
    // update buffers

    UpdateBuffers(drawData);


    BindShaderData(frameInfo);

    int vertexOffset = 0;
    uint indexOffset = 0;

    if (drawData.CmdListsCount > 0) {
      ulong[] offsets = [0];
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

          if (pcmd.TextureId == 0) {
            BindTexture(frameInfo);
          } else {
            /*
            // var target = _userTextures.Where(x => x.Value = pcmd.TextureId)
            foreach (var userTex in _userTextures) {
              if (userTex.Value == pcmd.TextureId) {
                Logger.Error($"{userTex.Key.TextureName} {_userTextures.Count}");
                BindTexture(frameInfo, userTex.Key.TextureDescriptor);
                break;
              }
            }
            */

            var target = _userTextures.TryGetValue(pcmd.TextureId, out var texture);
            if (target) {
              BindTexture(frameInfo, texture!.TextureDescriptor);
            }

          }


          SetScissorRect(frameInfo, pcmd, drawData);
          // vkCmdDrawIndexed(frameInfo.CommandBuffer, pcmd.ElemCount, 1, indexOffset, vertexOffset, 0);

          vkCmdDrawIndexed(
            frameInfo.CommandBuffer,
            pcmd.ElemCount,
            1,
            pcmd.IdxOffset + indexOffset,
            (int)pcmd.VtxOffset + vertexOffset,
            0
          );
        }
        indexOffset += (uint)cmdList.IdxBuffer.Size;
        vertexOffset += cmdList.VtxBuffer.Size;
      }
    }

  }

  public unsafe void Dispose() {
    foreach (var userTex in _userTextures) {
      MemoryUtils.FreeIntPtr(userTex.Key);
    }

    ImGui.DestroyContext();
    _vertexBuffer?.Dispose();
    _indexBuffer?.Dispose();

    vkDestroyImage(_device.LogicalDevice, _fontImage, null);
    vkDestroyImageView(_device.LogicalDevice, _fontView, null);
    vkFreeMemory(_device.LogicalDevice, _fontMemory, null);
    vkDestroySampler(_device.LogicalDevice, _sampler, null);

    _fontTexture?.Dispose();
    _systemPipeline?.Dispose();
    _systemDescriptorPool?.Dispose();
    _systemSetLayout?.Dispose();
    vkDestroyPipelineLayout(_device.LogicalDevice, _systemPipelineLayout);
    vkDestroyPipelineCache(_device.LogicalDevice, _pipelineCache, null);

    /*
    vkDestroyShaderModule(_device.LogicalDevice, _vertexModule, null);
    vkDestroyShaderModule(_device.LogicalDevice, _fragmentModule, null);


    vkDestroyPipeline(_device.LogicalDevice, _pipeline, null);
    vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout, null);
    vkDestroyDescriptorPool(_device.LogicalDevice, _descriptorPool, null);
    vkDestroyDescriptorSetLayout(_device.LogicalDevice, _descriptorSetLayout, null);
    */
  }

  public DescriptorPool GetDescriptorPool() {
    return _systemDescriptorPool;
  }

  public DescriptorSetLayout GetDescriptorSetLayout() {
    return _systemSetLayout;
  }

  public VkPipelineLayout GetPipelineLayout() {
    return _systemPipelineLayout;
  }

  public VkDescriptorSet GetDescriptorSet() {
    return _systemDescriptorSet;
  }
}
