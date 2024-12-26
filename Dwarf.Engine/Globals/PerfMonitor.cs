namespace Dwarf.Globals;

public static class PerfMonitor {
  public static uint TextureBindingsIn3DRenderer { get; set; }
  public static uint VertexBindingsIn3DRenderer { get; set; }
  public static uint NumberOfObjectsRenderedIn3DRenderer { get; set; }

  public static void Clear3DRendererInfo() {
    TextureBindingsIn3DRenderer = 0;
    VertexBindingsIn3DRenderer = 0;
    NumberOfObjectsRenderedIn3DRenderer = 0;
  }
}