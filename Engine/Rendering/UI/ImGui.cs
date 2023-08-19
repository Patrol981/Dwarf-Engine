using Dwarf.Vulkan;

using ImGuiNET;
using static ImGuiNET.ImGuiNative;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;
using DwarfEngine.Engine.UI;
using System.Numerics;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;
using static DwarfEngine.Engine.UI.ImGuiTools;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Dwarf.Extensions.Lists;
using System;
using OpenTK.Compute.OpenCL;
using OpenTK.Audio.OpenAL;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dwarf.Engine.Rendering.UI;

enum GLFWClientApi {
  Unknkown,
  OpenGL,
  Vulkan
}

struct ImGuiContext {

}

struct ImVec2 {
  public float x;
  public float y;
}

enum MouseCursorType {
  Arrow,
  TextInput,
  ResizeNS,
  ResizeEW,
  Hand,
  ResizeAll,
  ResizeNESW,
  ResizeNWSE,
  NotAllowed
}

unsafe struct ImGui_ImplVulkan_InitInfo {
  public VkInstance Instance;
  public VkPhysicalDevice PhysicalDevice;
  public VkDevice Device;
  public uint QueueFamily;
  public VkQueue Queue;
  public VkPipelineCache PipelineCache;
  public VkDescriptorPool DescriptorPool;
  public uint Subpass;
  public uint MinImageCount;          // >= 2
  public uint ImageCount;             // >= MinImageCount
  public VkSampleCountFlags MSAASamples;            // >= VK_SAMPLE_COUNT_1_BIT (0 -> default to VK_SAMPLE_COUNT_1_BIT)

  // Dynamic Rendering (Optional)
  public bool UseDynamicRendering;    // Need to explicitly enable VK_KHR_dynamic_rendering extension to use this, even for Vulkan 1.3.
  public VkFormat ColorAttachmentFormat;  // Required for dynamic rendering

  // Allocation, Debugging
  public VkAllocationCallbacks* Allocator;
}

struct ImGui_ImplVulkanH_FrameRenderBuffers {
  public VkDeviceMemory VertexBufferMemory;
  public VkDeviceMemory IndexBufferMemory;
  public uint VertexBufferSize;
  public uint IndexBufferSize;
  public VkBuffer VertexBuffer;
  public VkBuffer IndexBuffer;
};

unsafe struct ImGui_ImplVulkanH_WindowRenderBuffers {
  public uint Index;
  public uint Count;
  public ImGui_ImplVulkanH_FrameRenderBuffers* FrameRenderBuffers;
};

unsafe struct ImGui_ImplVulkan_Data {
  public ImGui_ImplVulkan_InitInfo VulkanInitInfo;
  public VkRenderPass RenderPass;
  public ulong BufferMemoryAlignment;
  public VkPipelineCreateFlags PipelineCreateFlags;
  public VkDescriptorSetLayout DescriptorSetLayout;
  public VkPipelineLayout PipelineLayout;
  public VkPipeline Pipeline;
  public uint Subpass;
  public VkShaderModule ShaderModuleVert;
  public VkShaderModule ShaderModuleFrag;

  // Font data
  public VkSampler FontSampler;
  public VkDeviceMemory FontMemory;
  public VkImage FontImage;
  public VkImageView FontView;
  public VkDescriptorSet FontDescriptorSet;
  public VkDeviceMemory UploadBufferMemory;
  public VkBuffer UploadBuffer;

  // Render buffers for main window
  public ImGui_ImplVulkanH_WindowRenderBuffers MainWindowRenderBuffers;

  /*
  ImGui_ImplVulkan_Data() {
    memset((void*)this, 0, sizeof(*this));
    BufferMemoryAlignment = 256;
  }
  */
}

unsafe struct ImGui_ImplGlfw_Data {
  public GLFWwindow* Window;
  public GLFWClientApi ClientApi;
  public double Time;
  public GLFWwindow* MouseWindow;
  public nint[] MouseCursors;
  public ImVec2 LastValidMousePos;
  public bool InstalledCallbacks;
  public bool CallbacksChainForAllWindows;

  // GLFWwindowfocusfun PrevUserCallbackWindowFocus;
  // GLFWcursorposfun PrevUserCallbackCursorPos;
  // GLFWcursorenterfun PrevUserCallbackCursorEnter;
  // GLFWmousebuttonfun PrevUserCallbackMousebutton;
  // GLFWscrollfun PrevUserCallbackScroll;
  // GLFWkeyfun PrevUserCallbackKey;
  // GLFWcharfun PrevUserCallbackChar;
  // GLFWmonitorfun PrevUserCallbackMonitor;

  // Chain GLFW callbacks: our callbacks will call the user's previously installed callbacks, if any.
  void* PrevUserCallbackWindowFocus;
  void* PrevUserCallbackCursorPos;
  void* PrevUserCallbackCursorEnter;
  void* PrevUserCallbackMousebutton;
  void* PrevUserCallbackScroll;
  void* PrevUserCallbackKey;
  void* PrevUserCallbackChar;
  void* PrevUserCallbackMonitor;
}

public unsafe class ImGuiRenderer : IDisposable {
  private readonly Device _device;
  private readonly Application _application;

  private int _windowWidth;
  private int _windowHeight;
  private Vector2 _scaleFactor = Vector2.One;

  private IntPtr _context;
  private GLFWwindow* _windowHandle;
  private ImGuiIO* _io;

  private VkDescriptorPool _pool;

  private static uint[] _glslShaderVertSpv = {
    0x07230203,0x00010000,0x00080001,0x0000002e,0x00000000,0x00020011,0x00000001,0x0006000b,
    0x00000001,0x4c534c47,0x6474732e,0x3035342e,0x00000000,0x0003000e,0x00000000,0x00000001,
    0x000a000f,0x00000000,0x00000004,0x6e69616d,0x00000000,0x0000000b,0x0000000f,0x00000015,
    0x0000001b,0x0000001c,0x00030003,0x00000002,0x000001c2,0x00040005,0x00000004,0x6e69616d,
    0x00000000,0x00030005,0x00000009,0x00000000,0x00050006,0x00000009,0x00000000,0x6f6c6f43,
    0x00000072,0x00040006,0x00000009,0x00000001,0x00005655,0x00030005,0x0000000b,0x0074754f,
    0x00040005,0x0000000f,0x6c6f4361,0x0000726f,0x00030005,0x00000015,0x00565561,0x00060005,
    0x00000019,0x505f6c67,0x65567265,0x78657472,0x00000000,0x00060006,0x00000019,0x00000000,
    0x505f6c67,0x7469736f,0x006e6f69,0x00030005,0x0000001b,0x00000000,0x00040005,0x0000001c,
    0x736f5061,0x00000000,0x00060005,0x0000001e,0x73755075,0x6e6f4368,0x6e617473,0x00000074,
    0x00050006,0x0000001e,0x00000000,0x61635375,0x0000656c,0x00060006,0x0000001e,0x00000001,
    0x61725475,0x616c736e,0x00006574,0x00030005,0x00000020,0x00006370,0x00040047,0x0000000b,
    0x0000001e,0x00000000,0x00040047,0x0000000f,0x0000001e,0x00000002,0x00040047,0x00000015,
    0x0000001e,0x00000001,0x00050048,0x00000019,0x00000000,0x0000000b,0x00000000,0x00030047,
    0x00000019,0x00000002,0x00040047,0x0000001c,0x0000001e,0x00000000,0x00050048,0x0000001e,
    0x00000000,0x00000023,0x00000000,0x00050048,0x0000001e,0x00000001,0x00000023,0x00000008,
    0x00030047,0x0000001e,0x00000002,0x00020013,0x00000002,0x00030021,0x00000003,0x00000002,
    0x00030016,0x00000006,0x00000020,0x00040017,0x00000007,0x00000006,0x00000004,0x00040017,
    0x00000008,0x00000006,0x00000002,0x0004001e,0x00000009,0x00000007,0x00000008,0x00040020,
    0x0000000a,0x00000003,0x00000009,0x0004003b,0x0000000a,0x0000000b,0x00000003,0x00040015,
    0x0000000c,0x00000020,0x00000001,0x0004002b,0x0000000c,0x0000000d,0x00000000,0x00040020,
    0x0000000e,0x00000001,0x00000007,0x0004003b,0x0000000e,0x0000000f,0x00000001,0x00040020,
    0x00000011,0x00000003,0x00000007,0x0004002b,0x0000000c,0x00000013,0x00000001,0x00040020,
    0x00000014,0x00000001,0x00000008,0x0004003b,0x00000014,0x00000015,0x00000001,0x00040020,
    0x00000017,0x00000003,0x00000008,0x0003001e,0x00000019,0x00000007,0x00040020,0x0000001a,
    0x00000003,0x00000019,0x0004003b,0x0000001a,0x0000001b,0x00000003,0x0004003b,0x00000014,
    0x0000001c,0x00000001,0x0004001e,0x0000001e,0x00000008,0x00000008,0x00040020,0x0000001f,
    0x00000009,0x0000001e,0x0004003b,0x0000001f,0x00000020,0x00000009,0x00040020,0x00000021,
    0x00000009,0x00000008,0x0004002b,0x00000006,0x00000028,0x00000000,0x0004002b,0x00000006,
    0x00000029,0x3f800000,0x00050036,0x00000002,0x00000004,0x00000000,0x00000003,0x000200f8,
    0x00000005,0x0004003d,0x00000007,0x00000010,0x0000000f,0x00050041,0x00000011,0x00000012,
    0x0000000b,0x0000000d,0x0003003e,0x00000012,0x00000010,0x0004003d,0x00000008,0x00000016,
    0x00000015,0x00050041,0x00000017,0x00000018,0x0000000b,0x00000013,0x0003003e,0x00000018,
    0x00000016,0x0004003d,0x00000008,0x0000001d,0x0000001c,0x00050041,0x00000021,0x00000022,
    0x00000020,0x0000000d,0x0004003d,0x00000008,0x00000023,0x00000022,0x00050085,0x00000008,
    0x00000024,0x0000001d,0x00000023,0x00050041,0x00000021,0x00000025,0x00000020,0x00000013,
    0x0004003d,0x00000008,0x00000026,0x00000025,0x00050081,0x00000008,0x00000027,0x00000024,
    0x00000026,0x00050051,0x00000006,0x0000002a,0x00000027,0x00000000,0x00050051,0x00000006,
    0x0000002b,0x00000027,0x00000001,0x00070050,0x00000007,0x0000002c,0x0000002a,0x0000002b,
    0x00000028,0x00000029,0x00050041,0x00000011,0x0000002d,0x0000001b,0x0000000d,0x0003003e,
    0x0000002d,0x0000002c,0x000100fd,0x00010038
  };

  private static uint[] _glslShaderFragSpv = {
    0x07230203,0x00010000,0x00080001,0x0000001e,0x00000000,0x00020011,0x00000001,0x0006000b,
    0x00000001,0x4c534c47,0x6474732e,0x3035342e,0x00000000,0x0003000e,0x00000000,0x00000001,
    0x0007000f,0x00000004,0x00000004,0x6e69616d,0x00000000,0x00000009,0x0000000d,0x00030010,
    0x00000004,0x00000007,0x00030003,0x00000002,0x000001c2,0x00040005,0x00000004,0x6e69616d,
    0x00000000,0x00040005,0x00000009,0x6c6f4366,0x0000726f,0x00030005,0x0000000b,0x00000000,
    0x00050006,0x0000000b,0x00000000,0x6f6c6f43,0x00000072,0x00040006,0x0000000b,0x00000001,
    0x00005655,0x00030005,0x0000000d,0x00006e49,0x00050005,0x00000016,0x78655473,0x65727574,
    0x00000000,0x00040047,0x00000009,0x0000001e,0x00000000,0x00040047,0x0000000d,0x0000001e,
    0x00000000,0x00040047,0x00000016,0x00000022,0x00000000,0x00040047,0x00000016,0x00000021,
    0x00000000,0x00020013,0x00000002,0x00030021,0x00000003,0x00000002,0x00030016,0x00000006,
    0x00000020,0x00040017,0x00000007,0x00000006,0x00000004,0x00040020,0x00000008,0x00000003,
    0x00000007,0x0004003b,0x00000008,0x00000009,0x00000003,0x00040017,0x0000000a,0x00000006,
    0x00000002,0x0004001e,0x0000000b,0x00000007,0x0000000a,0x00040020,0x0000000c,0x00000001,
    0x0000000b,0x0004003b,0x0000000c,0x0000000d,0x00000001,0x00040015,0x0000000e,0x00000020,
    0x00000001,0x0004002b,0x0000000e,0x0000000f,0x00000000,0x00040020,0x00000010,0x00000001,
    0x00000007,0x00090019,0x00000013,0x00000006,0x00000001,0x00000000,0x00000000,0x00000000,
    0x00000001,0x00000000,0x0003001b,0x00000014,0x00000013,0x00040020,0x00000015,0x00000000,
    0x00000014,0x0004003b,0x00000015,0x00000016,0x00000000,0x0004002b,0x0000000e,0x00000018,
    0x00000001,0x00040020,0x00000019,0x00000001,0x0000000a,0x00050036,0x00000002,0x00000004,
    0x00000000,0x00000003,0x000200f8,0x00000005,0x00050041,0x00000010,0x00000011,0x0000000d,
    0x0000000f,0x0004003d,0x00000007,0x00000012,0x00000011,0x0004003d,0x00000014,0x00000017,
    0x00000016,0x00050041,0x00000019,0x0000001a,0x0000000d,0x00000018,0x0004003d,0x0000000a,
    0x0000001b,0x0000001a,0x00050057,0x00000007,0x0000001c,0x00000017,0x0000001b,0x00050085,
    0x00000007,0x0000001d,0x00000012,0x0000001c,0x0003003e,0x00000009,0x0000001d,0x000100fd,
    0x00010038
  };

  public ImGuiRenderer(Device device, Application application) {
    _device = device;
    _application = application;
  }

  public unsafe void Init() {
    CreateDeviceResources();
    _windowHeight = (int)_application.Renderer.Extent2D.height;
    _windowWidth = (int)_application.Renderer.Extent2D.width;

    _context = igCreateContext(null);
    _io = igGetIO();

    GLFWImpl();

    ImGui_ImplVulkan_InitInfo info = new();
    info.Instance = _application.Device.VkInstance;
    info.PhysicalDevice = _application.Device.PhysicalDevice;
    info.Device = _application.Device.LogicalDevice;
    info.Queue = _application.Device.GraphicsQueue;
    info.DescriptorPool = _pool;
    info.MinImageCount = (uint)_application.Renderer.MAX_FRAMES_IN_FLIGHT;
    info.ImageCount = _application.Renderer.Swapchain.ImageCount;

    VulkanImpl(&info, _application.Renderer.GetSwapchainRenderPass());
    igStyleColorsDark(null);


    // _application.Renderer.Swapchain.SubmitCommandBuffers()

    // igNewFrame();
  }

  private unsafe void VulkanImpl(ImGui_ImplVulkan_InitInfo* info, VkRenderPass renderPass) {
    ImGuiIO* io = igGetIO();
    if (io->BackendRendererUserData != null) {
      Logger.Error("Already initialized a renderer  backend!");
    }

    if (info->Instance == VkInstance.Null) { Logger.Error("No Vulkan Instance found"); return; }
    if (info->PhysicalDevice == VkPhysicalDevice.Null) { Logger.Error("No Vulkan Physical Device found"); return; }
    if (info->Device == VkDevice.Null) { Logger.Error("No Vulkan Logical Device found"); return; }
    if (info->Queue == VkQueue.Null) { Logger.Error("No Vulkan Queue found"); return; }
    if (info->DescriptorPool == VkDescriptorPool.Null) { Logger.Error("No Vulkan Descriptor Pool found"); return; }
    if (info->MinImageCount < 2) { Logger.Error("Min Image Count is too small"); return; }
    if (info->ImageCount < info->MinImageCount) { Logger.Error("Image count is smaller than minimum"); return; }
    if (!info->UseDynamicRendering) {
      if (renderPass == VkRenderPass.Null) { Logger.Error("Render pass required"); return; }
    }

    ImGui_ImplVulkan_Data* bd = (ImGui_ImplVulkan_Data*)Marshal.AllocHGlobal(Unsafe.SizeOf<ImGui_ImplVulkan_Data>());
    io->BackendRendererUserData = bd;
    var backendName = "imgui_impl_vulkan";
    io->BackendRendererName = (byte*)&backendName;
    io->BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

    bd->VulkanInitInfo = *info;
    bd->RenderPass = renderPass;
    bd->Subpass = info->Subpass;

    bd->PipelineCreateFlags = VkPipelineCreateFlags.None;

    VulkanCreateDeviceObjects();
  }

  private unsafe void VulkanCreateDeviceObjects() {
    ImGui_ImplVulkan_Data* bd = ImGuiImplVulkanGetBackendData();
    ImGui_ImplVulkan_InitInfo* v = &bd->VulkanInitInfo;

    if (bd->FontSampler == VkSampler.Null || bd->FontSampler != VkSampler.Null) {
      Logger.Info("[ImGui] Creating Font Sampler");

      VkSamplerCreateInfo info = new();
      info.magFilter = VkFilter.Linear;
      info.minFilter = VkFilter.Linear;
      info.mipmapMode = VkSamplerMipmapMode.Linear;
      info.addressModeU = VkSamplerAddressMode.Repeat;
      info.addressModeV = VkSamplerAddressMode.Repeat;
      info.addressModeW = VkSamplerAddressMode.Repeat;
      info.minLod = -1000;
      info.maxLod = 1000;
      info.maxAnisotropy = 1.0f;
      vkCreateSampler(v->Device, &info, v->Allocator, &bd->FontSampler).CheckResult();
    }

    if (bd->DescriptorSetLayout == VkDescriptorSetLayout.Null || bd->DescriptorSetLayout != VkDescriptorSetLayout.Null) {
      Logger.Info("[ImGui] Creating Descriptor Set Layout");

      VkDescriptorSetLayoutBinding[] binding = new VkDescriptorSetLayoutBinding[1];
      binding[0].descriptorType = VkDescriptorType.CombinedImageSampler;
      binding[0].descriptorCount = 1;
      binding[0].stageFlags = VkShaderStageFlags.Fragment;
      VkDescriptorSetLayoutCreateInfo info = new();
      info.bindingCount = 1;
      fixed (VkDescriptorSetLayoutBinding* setLayoutBindingPtr = binding) {
        info.pBindings = setLayoutBindingPtr;
      }
      vkCreateDescriptorSetLayout(v->Device, &info, v->Allocator, &bd->DescriptorSetLayout).CheckResult();
    }

    if (bd->PipelineLayout == VkPipelineLayout.Null || bd->PipelineLayout != VkPipelineLayout.Null) {
      Logger.Info("[ImGui] Creating Pipeline Layout");

      // Constants: we are using 'vec2 offset' and 'vec2 scale' instead of a full 3d projection matrix
      VkPushConstantRange[] pushConstants = new VkPushConstantRange[1];

      pushConstants[0] = new();
      pushConstants[0].stageFlags = VkShaderStageFlags.Vertex;
      pushConstants[0].offset = sizeof(float) * 0;
      pushConstants[0].size = sizeof(float) * 4;
      VkDescriptorSetLayout[] setLayout = new VkDescriptorSetLayout[1];

      setLayout[0] = new();
      setLayout[0] = bd->DescriptorSetLayout;
      VkPipelineLayoutCreateInfo layoutInfo = new();
      layoutInfo.setLayoutCount = (uint)setLayout.Length;
      fixed (VkDescriptorSetLayout* lPtr = setLayout) {
        layoutInfo.pSetLayouts = lPtr;
      }
      layoutInfo.pushConstantRangeCount = 1;
      fixed (VkPushConstantRange* pPtr = pushConstants) {
        layoutInfo.pPushConstantRanges = pPtr;
      }
      vkCreatePipelineLayout(v->Device, &layoutInfo, v->Allocator, &bd->PipelineLayout).CheckResult();
    }

    // ImGui_ImplVulkan_CreatePipeline(v->Device, v->Allocator, v->PipelineCache, bd->RenderPass, v->MSAASamples, &bd->Pipeline, bd->Subpass);
    v->PipelineCache = VkPipelineCache.Null;
    CreatePipeline(v->Device, v->Allocator, v->PipelineCache, bd->RenderPass, v->MSAASamples, &bd->Pipeline, bd->Subpass);
  }

  private unsafe void CreateShaderModules(VkDevice device, VkAllocationCallbacks* allocator) {
    var vertexPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders", "imgui_vertex.spv");
    var fragmentPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders", "imgui_fragment.spv");
    var vertexCode = File.ReadAllBytes(vertexPath);
    var fragmentCode = File.ReadAllBytes(fragmentPath);

    vkCreateShaderModule(_device.LogicalDevice, vertexCode, null, out var vertex).CheckResult();
    vkCreateShaderModule(_device.LogicalDevice, fragmentCode, null, out var fragment).CheckResult();

    ImGui_ImplVulkan_Data* bd = ImGuiImplVulkanGetBackendData();
    bd->ShaderModuleVert = vertex;
    bd->ShaderModuleFrag = fragment;

    /*
    ImGui_ImplVulkan_Data* bd = ImGuiImplVulkanGetBackendData();
    if (bd->ShaderModuleVert == VkShaderModule.Null) {
      VkShaderModuleCreateInfo vert_info = new();
      vert_info.codeSize = (nuint)(_glslShaderVertSpv.Length * sizeof(uint));
      fixed (uint* ptr = _glslShaderVertSpv) {
        vert_info.pCode = ptr;
      }
      vkCreateShaderModule(device, &vert_info, allocator, &bd->ShaderModuleVert).CheckResult();
    }
    if (bd->ShaderModuleFrag == VkShaderModule.Null) {
      VkShaderModuleCreateInfo frag_info = new();
      frag_info.codeSize = (nuint)(_glslShaderFragSpv.Length * sizeof(uint));
      fixed (uint* ptr = _glslShaderFragSpv) {
        frag_info.pCode = ptr;
      }
      vkCreateShaderModule(device, &frag_info, allocator, &bd->ShaderModuleFrag).CheckResult();
    }
    */
  }

  private unsafe void CreatePipeline(VkDevice device, VkAllocationCallbacks* allocator, VkPipelineCache pipelineCache, VkRenderPass renderPass, VkSampleCountFlags samples, VkPipeline* pipeline, uint subpass) {
    ImGui_ImplVulkan_Data* bd = ImGuiImplVulkanGetBackendData();
    CreateShaderModules(device, allocator);

    VkPipelineShaderStageCreateInfo[] stage = new VkPipelineShaderStageCreateInfo[2];
    VkString entryPoint = new("main");

    stage[0] = new();
    stage[0].stage = VkShaderStageFlags.Vertex;
    stage[0].module = bd->ShaderModuleVert;
    stage[0].pName = entryPoint;

    stage[1] = new();
    stage[1].stage = VkShaderStageFlags.Fragment;
    stage[1].module = bd->ShaderModuleFrag;
    stage[1].pName = entryPoint;

    VkVertexInputBindingDescription[] binding_desc = new VkVertexInputBindingDescription[1];
    binding_desc[0] = new();
    binding_desc[0].stride = (uint)Unsafe.SizeOf<ImDrawVert>();
    binding_desc[0].inputRate = VkVertexInputRate.Vertex;

    VkVertexInputAttributeDescription[] attribute_desc = new VkVertexInputAttributeDescription[3];
    attribute_desc[0] = new();
    attribute_desc[0].location = 0;
    attribute_desc[0].binding = binding_desc[0].binding;
    attribute_desc[0].format = VkFormat.R32G32Sfloat;
    attribute_desc[0].offset = (uint)Marshal.OffsetOf<ImDrawVert>("pos"); // IM_OFFSETOF(ImDrawVert, pos);

    attribute_desc[1] = new();
    attribute_desc[1].location = 1;
    attribute_desc[1].binding = binding_desc[0].binding;
    attribute_desc[1].format = VkFormat.R32G32Sfloat;
    attribute_desc[1].offset = (uint)Marshal.OffsetOf<ImDrawVert>("uv"); // IM_OFFSETOF(ImDrawVert, uv);

    attribute_desc[2] = new();
    attribute_desc[2].location = 2;
    attribute_desc[2].binding = binding_desc[0].binding;
    attribute_desc[2].format = VkFormat.R8G8B8A8Unorm;
    attribute_desc[2].offset = (uint)Marshal.OffsetOf<ImDrawVert>("col"); // IM_OFFSETOF(ImDrawVert, col);

    VkPipelineVertexInputStateCreateInfo vertex_info = new();
    vertex_info.vertexBindingDescriptionCount = 1;
    fixed (VkVertexInputBindingDescription* bPtr = binding_desc) {
      vertex_info.pVertexBindingDescriptions = bPtr;
    }
    fixed (VkVertexInputAttributeDescription* aPtr = attribute_desc) {
      vertex_info.pVertexAttributeDescriptions = aPtr;
    }
    vertex_info.vertexAttributeDescriptionCount = 3;

    VkPipelineInputAssemblyStateCreateInfo ia_info = new();
    ia_info.topology = VkPrimitiveTopology.TriangleList;

    VkPipelineViewportStateCreateInfo viewport_info = new();
    viewport_info.viewportCount = 1;
    viewport_info.scissorCount = 1;

    VkPipelineRasterizationStateCreateInfo raster_info = new();
    raster_info.polygonMode = VkPolygonMode.Fill;
    raster_info.cullMode = VkCullModeFlags.None;
    raster_info.frontFace = VkFrontFace.CounterClockwise;
    raster_info.lineWidth = 1.0f;

    VkPipelineMultisampleStateCreateInfo ms_info = new();
    ms_info.rasterizationSamples = (samples != 0) ? samples : VkSampleCountFlags.Count1;

    VkPipelineColorBlendAttachmentState[] color_attachment = new VkPipelineColorBlendAttachmentState[1];
    color_attachment[0] = new();
    color_attachment[0].blendEnable = VkBool32.True;
    color_attachment[0].srcColorBlendFactor = VkBlendFactor.SrcAlpha; // VK_BLEND_FACTOR_SRC_ALPHA;
    color_attachment[0].dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha; // VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
    color_attachment[0].colorBlendOp = VkBlendOp.Add; // VK_BLEND_OP_ADD;
    color_attachment[0].srcAlphaBlendFactor = VkBlendFactor.One; // VK_BLEND_FACTOR_ONE;
    color_attachment[0].dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha; // VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
    color_attachment[0].alphaBlendOp = VkBlendOp.Add; // VK_BLEND_OP_ADD;
    color_attachment[0].colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A;
    // VK_COLOR_COMPONENT_R_BIT | VK_COLOR_COMPONENT_G_BIT | VK_COLOR_COMPONENT_B_BIT | VK_COLOR_COMPONENT_A_BIT;

    VkPipelineDepthStencilStateCreateInfo depth_info = new();

    VkPipelineColorBlendStateCreateInfo blend_info = new();
    blend_info.attachmentCount = 1;
    fixed (VkPipelineColorBlendAttachmentState* cPtr = color_attachment) {
      blend_info.pAttachments = cPtr;
    }

    VkDynamicState[] dynamic_states = { VkDynamicState.Viewport, VkDynamicState.Scissor };
    VkPipelineDynamicStateCreateInfo dynamic_state = new();
    dynamic_state.dynamicStateCount = (uint)dynamic_states.Length;
    fixed (VkDynamicState* dPtr = dynamic_states) {
      dynamic_state.pDynamicStates = dPtr;
    }

    VkGraphicsPipelineCreateInfo info = new();

    VkPipelineCreateFlags createFlags =
      VkPipelineCreateFlags.ViewIndexFromDeviceIndex |
      VkPipelineCreateFlags.DeferCompileNV |
      VkPipelineCreateFlags.CaptureStatisticsKHR |
      VkPipelineCreateFlags.CaptureInternalRepresentationsKHR |
      // VkPipelineCreateFlags.FailOnPipelineCompileRequired |
      VkPipelineCreateFlags.IndirectBindableNV |
      VkPipelineCreateFlags.RetainLinkTimeOptimizationInfoEXT |
      VkPipelineCreateFlags.ColorAttachmentFeedbackLoopEXT |
      VkPipelineCreateFlags.DepthStencilAttachmentFeedbackLoopEXT;
    // VkPipelineCreateFlags.ProtectedAccessOnlyEXT;

    // info.flags = bd->PipelineCreateFlags;
    info.flags = createFlags;
    info.stageCount = 2;
    fixed (VkPipelineShaderStageCreateInfo* sPtr = stage) {
      info.pStages = sPtr;
    }
    info.pVertexInputState = &vertex_info;
    info.pInputAssemblyState = &ia_info;
    info.pViewportState = &viewport_info;
    info.pRasterizationState = &raster_info;
    info.pMultisampleState = &ms_info;
    info.pDepthStencilState = &depth_info;
    info.pColorBlendState = &blend_info;
    info.pDynamicState = &dynamic_state;
    info.layout = bd->PipelineLayout;
    info.renderPass = renderPass;
    info.subpass = subpass;

    // dynamic render
    /*
    VkPipelineRenderingCreateInfo pipelineRenderingCreateInfo = new();
    pipelineRenderingCreateInfo.colorAttachmentCount = 1;
    pipelineRenderingCreateInfo.pColorAttachmentFormats = &bd->VulkanInitInfo.ColorAttachmentFormat;
    if (bd->VulkanInitInfo.UseDynamicRendering) {
      info.pNext = &pipelineRenderingCreateInfo;
      info.renderPass = VkRenderPass.Null; // Just make sure it's actually nullptr.
    }
    */

    vkCreateGraphicsPipelines(device, VkPipelineCache.Null, 1, &info, allocator, pipeline).CheckResult();
  }

  private unsafe void GLFWImpl() {
    ImGuiIO* io = igGetIO();
    if (io->BackendPlatformUserData != null) {
      Logger.Error("Already initialized a platform backend!");
      return;
    }

    ImGui_ImplGlfw_Data* bd = (ImGui_ImplGlfw_Data*)Marshal.AllocHGlobal(Unsafe.SizeOf<ImGui_ImplGlfw_Data>());
    io->BackendPlatformUserData = bd;
    var backendName = "imgui_impl_glfw";
    io->BackendPlatformName = (byte*)&backendName;
    io->BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
    io->BackendFlags |= ImGuiBackendFlags.HasSetMousePos;

    bd->Window = _application.Window.GLFWwindow;
    bd->Time = 0.0f;

    // TODO : add clipboard functionality
    io->SetClipboardTextFn = IntPtr.Zero;
    io->GetClipboardTextFn = IntPtr.Zero;
    io->ClipboardUserData = bd->Window;

    bd->MouseCursors = new nint[(int)MouseCursorType.NotAllowed + 1];
    bd->MouseCursors[(int)MouseCursorType.Arrow] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.TextInput] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.ResizeNS] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.ResizeEW] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.Hand] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.ResizeAll] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.ResizeNESW] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.ResizeNWSE] = _application.Window.CursorHandle;
    bd->MouseCursors[(int)MouseCursorType.NotAllowed] = _application.Window.CursorHandle;

    // TODO : install callbacks functionallity
    ImGuiViewport* mainViewport = igGetMainViewport();

    mainViewport->PlatformHandleRaw = (void*)GLFW.glfwGetWin32Window(bd->Window);
    bd->ClientApi = GLFWClientApi.Vulkan;
  }

  public void Update(FrameInfo frameInfo) {

  }
  public void Update() {
    NewFrameVulkan();
    NewFrameGLFW();

    // igNewFrame();
    // igBegin("Test", null, 0);
  }

  private unsafe void NewFrameVulkan() {
    ImGui_ImplVulkan_Data* bd = ImGuiImplVulkanGetBackendData();
    if (bd == null) {
      Logger.Error("[ImGui] NewFrame Error. Did you call ImGui_ImplVulkan_Init()?");
    }
  }

  private unsafe void NewFrameGLFW() {
    var io = igGetIO();
    ImGui_ImplGlfw_Data* bd = ImGuiImplGLFWGetBackendData();
    if (bd == null) {
      Logger.Error("[ImGui] NewFrame Error. Did you call ImGui_ImplGlfw_InitForXXX()?");
    }

    int w, h;
    int displayW, displayH;
    GLFW.glfwGetWindowSize(bd->Window, out w, out h);
    GLFW.glfwGetFramebufferSize(bd->Window, out displayW, out displayH);
    io->DisplaySize = new((float)w, (float)h);
    if (w > 0 && h > 0) {
      io->DisplayFramebufferScale = new((float)displayW / (float)w, (float)displayH / (float)h);
    }

    // Setup time step
    // (Accept glfwGetTime() not returning a monotonically increasing value. Seems to happens on disconnecting peripherals and probably on VMs and Emscripten, see #6491, #6189, #6114, #3644)
    double current_time = GLFW.glfwGetTime();
    if (current_time <= bd->Time)
      current_time = bd->Time + 0.00001f;
    io->DeltaTime = bd->Time > 0.0 ? (float)(current_time - bd->Time) : (float)(1.0f / 60.0f);
    bd->Time = current_time;

    // ImGui_ImplGlfw_UpdateMouseData();
    // ImGui_ImplGlfw_UpdateMouseCursor();

    // Update game controllers (if enabled and available)
    // ImGui_ImplGlfw_UpdateGamepads();
  }

  private void SetPerFrameImGuiData(float deltaSeconds) {
    ImGuiIOPtr io = ImGui.GetIO();
    io.DisplaySize = new Vector2(
        _windowWidth / _scaleFactor.X,
        _windowHeight / _scaleFactor.Y);
    io.DisplayFramebufferScale = _scaleFactor;
    io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
  }

  private unsafe void CreateDeviceResources() {
    VkDescriptorPoolSize[] poolSizes = {
      new(VkDescriptorType.Sampler, 1000),
      new(VkDescriptorType.CombinedImageSampler, 1000),
      new(VkDescriptorType.SampledImage, 1000),
      new(VkDescriptorType.StorageImage, 1000),
      new(VkDescriptorType.UniformTexelBuffer, 1000),
      new(VkDescriptorType.StorageTexelBuffer, 1000),
      new(VkDescriptorType.UniformBuffer, 1000),
      new(VkDescriptorType.StorageBuffer, 1000),
      new(VkDescriptorType.UniformBufferDynamic, 1000),
      new(VkDescriptorType.StorageBufferDynamic, 1000),
      new(VkDescriptorType.InputAttachment, 1000)
    };

    VkDescriptorPoolCreateInfo poolInfo = new();
    poolInfo.flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet;
    poolInfo.maxSets = 1000;
    poolInfo.poolSizeCount = (uint)poolSizes.Length;
    fixed (VkDescriptorPoolSize* ptr = poolSizes) {
      poolInfo.pPoolSizes = ptr;
    }

    vkCreateDescriptorPool(_device.LogicalDevice, poolSizes, 1000, out _pool);
  }

  private unsafe void DestroyFontUploadObjects() {
    ImGuiIO* io = igGetIO();
    var vulkanData = ImGuiImplVulkanGetBackendData();
    var v = &vulkanData->VulkanInitInfo;
    if (vulkanData->UploadBuffer != VkBuffer.Null) {
      vkDestroyBuffer(v->Device, vulkanData->UploadBuffer, v->Allocator);
      vulkanData->UploadBuffer = VkBuffer.Null;
    }
    if (vulkanData->UploadBufferMemory != VkBuffer.Null) {
      vkFreeMemory(v->Device, vulkanData->UploadBufferMemory, v->Allocator);
      vulkanData->UploadBufferMemory = VkDeviceMemory.Null;
    }
  }

  private unsafe void DestroyFrameRenderBuffers(VkDevice device, ImGui_ImplVulkanH_FrameRenderBuffers* buffers, VkAllocationCallbacks* allocator) {
    if (buffers->VertexBuffer != VkBuffer.Null) {
      vkDestroyBuffer(device, buffers->VertexBuffer, allocator);
      buffers->VertexBuffer = VkBuffer.Null;
    }
    if (buffers->VertexBufferMemory != VkBuffer.Null) {
      // vkFreeMemory(device, buffers->VertexBufferMemory);
      // buffers->VertexBufferMemory = VkDeviceMemory.Null;
    }
    if (buffers->IndexBuffer != VkBuffer.Null) {
      vkDestroyBuffer(device, buffers->IndexBuffer, allocator);
      buffers->IndexBuffer = VkBuffer.Null;
    }
    if (buffers->IndexBufferMemory != VkBuffer.Null) {
      // vkFreeMemory(device, buffers->IndexBufferMemory, allocator);
      // buffers->IndexBufferMemory = VkDeviceMemory.Null;
    }
    buffers->VertexBufferSize = 0;
    buffers->IndexBufferSize = 0;
  }

  private unsafe void DestroyBuffers(VkDevice device, ImGui_ImplVulkanH_WindowRenderBuffers* buffers, VkAllocationCallbacks* allocator) {

    Logger.Info($"[Buffer] Count {buffers->Count}");
    for (uint n = 0; n < buffers->Count; n++) {
      DestroyFrameRenderBuffers(device, &buffers->FrameRenderBuffers[n], allocator);
    }

    // ImGui_ImplVulkanH_DestroyFrameRenderBuffers(device, &buffers->FrameRenderBuffers[n], allocator);
    Marshal.FreeHGlobal((nint)buffers->FrameRenderBuffers);
    // IM_FREE(buffers->FrameRenderBuffers);
    buffers->FrameRenderBuffers = (ImGui_ImplVulkanH_FrameRenderBuffers*)IntPtr.Zero;
    buffers->Index = 0;
    buffers->Count = 0;
  }

  private unsafe void AddFontDefault() {

  }

  private unsafe void Build(ImFontAtlas* fonts) {
    if (fonts->Locked == 1) {
      Logger.Error("Cannot modify a locked ImFontAtlas between NewFrame() and EndFrame/Render()!");
      return;
    }

    if (fonts->ConfigData.Size == 0) {
      AddFontDefault();
    }

    var builderIO = fonts->FontBuilderIO;
    if (builderIO == null) {

    }
  }

  private unsafe void GetTexDataAsAlpha8(ImFontAtlas* fonts, char** outPixels, int* outWidth, int* outHeight, int* outBytesPerPixels) {
    if (fonts->TexPixelsAlpha8 == null) {
      Build(fonts);
    }

    *outPixels = (char*)fonts->TexPixelsAlpha8;
    if (outWidth != null) *outWidth = fonts->TexWidth;
    if (outHeight != null) *outHeight = fonts->TexHeight;
    if (outBytesPerPixels != null) *outBytesPerPixels = 1;
  }

  private unsafe void GetTexDataAsRGBA32(ImFontAtlas* fonts, char** outPixels, int* outWidth, int* outHeight, int* outBytesPerPixels) {
    if (fonts->TexPixelsRGBA32 == null) {
      char* pixels = null;
      GetTexDataAsAlpha8(fonts, outPixels, null, null, null);
    }
  }

  private unsafe void ImGuiImplVulkanCreateFontsTexture(VkCommandBuffer commandBuffer) {
    var io = igGetIO();
    ImGui_ImplVulkan_Data* bd = ImGuiImplVulkanGetBackendData();
    ImGui_ImplVulkan_InitInfo* v = &bd->VulkanInitInfo;

    char* pixels;
    int outBytes;
    int width, height;
    GetTexDataAsRGBA32(io->Fonts, &pixels, &width, &height, &outBytes);

  }

  public unsafe void Dispose() {
    // 946 line
    Logger.Info("Disposing ImGui");
    ImGuiIO* io = igGetIO();
    var vulkanData = ImGuiImplVulkanGetBackendData();
    var v = &vulkanData->VulkanInitInfo;

    // DestroyBuffers(v->Device, &vulkanData->MainWindowRenderBuffers, v->Allocator);
    // DestroyFontUploadObjects();

    if (vulkanData->ShaderModuleVert != VkShaderModule.Null) {
      vkDestroyShaderModule(v->Device, vulkanData->ShaderModuleVert, v->Allocator);
      vulkanData->ShaderModuleVert = VkShaderModule.Null;
    }
    if (vulkanData->ShaderModuleFrag != VkShaderModule.Null) {
      vkDestroyShaderModule(v->Device, vulkanData->ShaderModuleFrag.Handle, v->Allocator);
      vulkanData->ShaderModuleFrag = VkShaderModule.Null;
    }
    if (vulkanData->FontView != VkImageView.Null) {
      vkDestroyImageView(v->Device, vulkanData->FontView.Handle, v->Allocator);
      vulkanData->FontView = VkImageView.Null;
    }
    if (vulkanData->FontMemory != VkDeviceMemory.Null) {
      vkFreeMemory(v->Device, vulkanData->FontMemory.Handle, v->Allocator);
      vulkanData->FontMemory = VkDeviceMemory.Null;
    }
    if (vulkanData->FontSampler != VkSampler.Null) {
      vkDestroySampler(v->Device, vulkanData->FontSampler.Handle, v->Allocator);
      vulkanData->FontSampler = VkSampler.Null;
    }
    if (vulkanData->DescriptorSetLayout != VkDescriptorSetLayout.Null) {
      vkDestroyDescriptorSetLayout(v->Device, vulkanData->DescriptorSetLayout.Handle, v->Allocator);
      vulkanData->DescriptorSetLayout = VkDescriptorSetLayout.Null;
    }
    if (vulkanData->PipelineLayout != VkPipelineLayout.Null) {
      vkDestroyPipelineLayout(v->Device, vulkanData->PipelineLayout.Handle, v->Allocator);
      vulkanData->PipelineLayout = VkPipelineLayout.Null;
    }
    if (vulkanData->Pipeline != VkPipeline.Null) {
      vkDestroyPipeline(v->Device, vulkanData->Pipeline.Handle, v->Allocator);
      vulkanData->Pipeline = VkPipeline.Null;
    }
    Marshal.FreeHGlobal((nint)io->BackendPlatformUserData);
    Marshal.FreeHGlobal((nint)io->BackendRendererUserData);
    vkDestroyDescriptorPool(_device.LogicalDevice, _pool);
  }

  internal unsafe ImGui_ImplVulkan_Data* ImGuiImplVulkanGetBackendData() {
    ImGuiIO* io = igGetIO();
    return (ImGui_ImplVulkan_Data*)io->BackendRendererUserData;
  }

  internal unsafe ImGui_ImplGlfw_Data* ImGuiImplGLFWGetBackendData() {
    ImGuiIO* io = igGetIO();
    return (ImGui_ImplGlfw_Data*)io->BackendPlatformUserData;
  }
}
