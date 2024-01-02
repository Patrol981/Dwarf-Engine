using System;

namespace Dwarf;

[Flags]
public enum BufferUsage {
  None = 0,
  //
  // Summary:
  //     Can be used as a source of transfer operations
  TransferSrc = 1,
  //
  // Summary:
  //     Can be used as a destination of transfer operations
  TransferDst = 2,
  //
  // Summary:
  //     Can be used as TBO
  UniformTexelBuffer = 4,
  //
  // Summary:
  //     Can be used as IBO
  StorageTexelBuffer = 8,
  //
  // Summary:
  //     Can be used as UBO
  UniformBuffer = 0x10,
  //
  // Summary:
  //     Can be used as SSBO
  StorageBuffer = 0x20,
  //
  // Summary:
  //     Can be used as source of fixed-function index fetch (index buffer)
  IndexBuffer = 0x40,
  //
  // Summary:
  //     Can be used as source of fixed-function vertex fetch (VBO)
  VertexBuffer = 0x80,
  //
  // Summary:
  //     Can be the source of indirect parameters (e.g. indirect buffer, parameter buffer)
  IndirectBuffer = 0x100,
  ShaderDeviceAddress = 0x20000,
  VideoDecodeSrcKHR = 0x2000,
  VideoDecodeDstKHR = 0x4000,
  TransformFeedbackBufferEXT = 0x800,
  TransformFeedbackCounterBufferEXT = 0x1000,
  ConditionalRenderingEXT = 0x200,
  ExecutionGraphScratchAMDX = 0x2000000,
  AccelerationStructureBuildInputReadOnlyKHR = 0x80000,
  AccelerationStructureStorageKHR = 0x100000,
  ShaderBindingTableKHR = 0x400,
  VideoEncodeDstKHR = 0x8000,
  VideoEncodeSrcKHR = 0x10000,
  SamplerDescriptorBufferEXT = 0x200000,
  ResourceDescriptorBufferEXT = 0x400000,
  PushDescriptorsDescriptorBufferEXT = 0x4000000,
  MicromapBuildInputReadOnlyEXT = 0x800000,
  MicromapStorageEXT = 0x1000000,
  RayTracingNV = 0x400,
  ShaderDeviceAddressEXT = 0x20000,
  ShaderDeviceAddressKHR = 0x20000
}