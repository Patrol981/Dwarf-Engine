﻿using Dwarf.Rendering;

using Vortice.Vulkan;

namespace Dwarf.Physics;
public interface IDebugRender3DObject : IDrawable {
  public bool UsesTexture { get; }
  public bool UsesLight { get; set; }
  public int MeshsesCount { get; }
  public bool FinishedInitialization { get; }
  public void BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout);
  public bool Enabled { get; }
  public void Enable();
  public void Disable();
}
