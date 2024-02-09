using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.Engine.Globals;
using Dwarf.Vulkan;

using DwarfEngine.Vulkan;

using OpenTK.Mathematics;

using SharpGLTF.Schema2;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Engine.Rendering;
public class Skybox : IDisposable {
  protected class SkyboxMesh {
    public TexturedVertex[] Vertices = [];
  }

  private readonly Device _device;
  private readonly TextureManager _textureManager;
  private readonly Renderer _renderer;
  private readonly float[] _vertices = [
    // positions
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,

    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,

    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,

    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,

    -1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    -1.0f,

    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f
  ];

  private readonly float[] _uvs = [
    // Front
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,

    // Left
    1.0f,
    0.0f,
    0.0f,
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    0.0f,

    // Right
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    0.0f,

    // Back
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,

    // Top
    0.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    0.0f,
    0.0f,
    1.0f,

    // Bottom
    0.0f,
    0.0f,
    0.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    1.0f,
    1.0f,
    1.0f
  ];
  private SkyboxMesh _mesh;
  private Transform _transform;
  private Material _material;

  private VkDescriptorSetLayout _globalDescriptorSetLayout;
  private PipelineConfigInfo _pipelineConfigInfo = null!;
  private VkPipelineLayout _pipelineLayout;
  private Pipeline _pipeline = null!;

  private DescriptorPool _descriptorPool = null!;
  private DescriptorPool _texturePool = null!;
  private DescriptorSetLayout _textureSetLayout = null!;

  private VkDescriptorSet _textureSet = VkDescriptorSet.Null;
  private Vulkan.Buffer _skyboxBuffer = null!;
  private Vulkan.Buffer _vertexBuffer = null!;
  private ulong _vertexCount = 0;

  private string[] _cubemapNames = new string[6];
  private CubeMapTexture _cubemapTexture = null!;

  public Skybox(Device device, TextureManager textureManager, Renderer renderer, VkDescriptorSetLayout globalSetLayout) {
    _device = device;
    _textureManager = textureManager;
    _renderer = renderer;
    _transform = new();
    _material = new(new(1.0f, 1.0f, 1.0f));

    _textureSetLayout = new DescriptorSetLayout.Builder(_device)
    .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    .Build();

    var meshVertices = new List<TexturedVertex>();
    for (int i = 0, j = 0; i < _vertices.Length; i += 3, j += 2) {
      meshVertices.Add(new() {
        Position = new(_vertices[i], _vertices[i + 1], _vertices[i + 2]),
        Uv = new(_uvs[j], _uvs[j + 1])
      });
    }

    _mesh = new() {
      Vertices = [.. meshVertices]
    };

    var dir = Dwarf.Utils.DwarfPath.AssemblyDirectory;
    _cubemapNames[0] = $"{dir}/Resources/skyboxes/sunny/right.jpg";
    _cubemapNames[1] = $"{dir}/Resources/skyboxes/sunny/left.jpg";
    _cubemapNames[2] = $"{dir}/Resources/skyboxes/sunny/bottom.jpg";
    _cubemapNames[3] = $"{dir}/Resources/skyboxes/sunny/top.jpg";
    _cubemapNames[4] = $"{dir}/Resources/skyboxes/sunny/front.jpg";
    _cubemapNames[5] = $"{dir}/Resources/skyboxes/sunny/back.jpg";

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      _textureSetLayout.GetDescriptorSetLayout(),
      globalSetLayout,
    ];

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(_renderer.GetSwapchainRenderPass(), "skybox_vertex", "skybox_fragment", new PipelineSkyboxProvider());

    InitCubeMapTexture();
  }

  private async void InitCubeMapTexture() {
    var data = await CubeMapTexture.LoadDataFromPath(_cubemapNames[0]);
    _cubemapTexture = new CubeMapTexture(_device, data.Width, data.Height, _cubemapNames, "cubemap0");

    CreateVertexBuffer(_mesh.Vertices);
    CreateBuffers();
    BindDescriptorTexture();
  }

  public unsafe void Render(FrameInfo frameInfo) {
    _pipeline.Bind(frameInfo.CommandBuffer);

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      _pipelineLayout,
      1,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    var pushConstantData = new SkyboxBufferObject {
      SkyboxMatrix = _transform.Matrix4,
      SkyboxColor = _material.GetColor()
    };

    vkCmdPushConstants(
      frameInfo.CommandBuffer,
      _pipelineLayout,
      VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      0,
      (uint)Unsafe.SizeOf<SkyboxBufferObject>(),
       &pushConstantData
    );

    Bind(frameInfo.CommandBuffer);
    BindTexture(frameInfo);
    Draw(frameInfo.CommandBuffer, (uint)_vertexCount);
  }

  private unsafe void BindTexture(FrameInfo frameInfo) {
    Descriptor.BindDescriptorSet(_textureSet, frameInfo, ref _pipelineLayout, 0, 1);
  }

  private unsafe void BindDescriptorTexture() {
    VkDescriptorImageInfo imageInfo = new() {
      sampler = _cubemapTexture.GetSampler(),
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = _cubemapTexture.GetImageView()
    };
    _ = new DescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out VkDescriptorSet set);
    _textureSet = set;
  }

  private unsafe void Bind(VkCommandBuffer commandBuffer) {
    VkBuffer[] buffers = [_vertexBuffer.GetBuffer()];
    ulong[] offsets = [0];
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }
  }

  private void Draw(VkCommandBuffer commandBuffer, uint stride) {
    vkCmdDraw(commandBuffer, stride, 1, 0, 0);
  }

  private void CreateVertexBuffer(TexturedVertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;

    ulong bufferSize = ((ulong)Unsafe.SizeOf<TexturedVertex>()) * _vertexCount;
    ulong vertexSize = (ulong)Unsafe.SizeOf<TexturedVertex>();

    var stagingBuffer = new Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount,
      VkBufferUsageFlags.TransferSrc,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    stagingBuffer.WriteToBuffer(VkUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new Vulkan.Buffer(
      _device,
      vertexSize,
      _vertexCount,
      VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst,
      VkMemoryPropertyFlags.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  private void CreateBuffers() {
    _descriptorPool = new DescriptorPool.Builder(_device)
      .SetMaxSets(1000)
      .AddPoolSize(VkDescriptorType.UniformBufferDynamic, 1000)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturePool = new DescriptorPool.Builder(_device)
      .SetMaxSets(6)
      .AddPoolSize(VkDescriptorType.CombinedImageSampler, 1)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _skyboxBuffer = new Vulkan.Buffer(
      _device,
      (ulong)Unsafe.SizeOf<SkyboxBufferObject>(),
      1,
      VkBufferUsageFlags.UniformBuffer,
      VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent,
      _device.Properties.limits.minUniformBufferOffsetAlignment
    );
  }

  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    var pipelineInfo = new VkPipelineLayoutCreateInfo() {
      setLayoutCount = (uint)layouts.Length
    };
    fixed (VkDescriptorSetLayout* layoutsPtr = layouts) {
      pipelineInfo.pSetLayouts = layoutsPtr;
    }
    VkPushConstantRange pushConstantRange = new() {
      stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      offset = 0,
      size = (uint)Unsafe.SizeOf<SkyboxBufferObject>()
    };
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private unsafe void CreatePipeline(
    VkRenderPass renderPass,
    string vertexName,
    string fragmentName,
    PipelineProvider pipelineProvider
  ) {
    _pipeline?.Dispose();
    _pipelineConfigInfo ??= new SkyboxPipeline();
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    _pipeline = new Pipeline(_device, vertexName, fragmentName, pipelineConfig, pipelineProvider);
  }

  protected unsafe void Dispose(bool disposing) {
    if (disposing) {
      vkQueueWaitIdle(_device.GraphicsQueue);
      _textureSetLayout?.Dispose();
      _descriptorPool?.Dispose();
      _texturePool?.Dispose();
      _pipeline?.Dispose();
      _vertexBuffer?.Dispose();
      _skyboxBuffer?.Dispose();
      vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
      _cubemapTexture.Dispose();
    }
  }

  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
}
