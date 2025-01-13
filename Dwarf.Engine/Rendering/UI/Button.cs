using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Extensions.Logging;

using Vortice.Vulkan;

namespace Dwarf.Rendering.UI;
public class Button : Component, I2DCollision, IUIElement {
  private readonly Application _application = null!;

  private GuiTexture _guiTexture = null!;
  private Vector3 _lastKnownScale = Vector3.Zero;
  private Vector2 _cachedSize = Vector2.Zero;
  private Bounds2D _cachedBounds = Bounds2D.Zero;

  public Button() {
    throw new Exception("Cannot create button without needed params");
  }
  public Button(Application application, string texturePath) {
    _application = application;
    _guiTexture = new GuiTexture(application.VmaAllocator, application.Device);
    _guiTexture.BindToTexture(_application.TextureManager, texturePath, false);
  }

  public void CheckCollision(object sender, EventArgs e) {
    var camera = CameraState.GetCamera();
    var size = Application.Instance.Window.Extent;
    var collResult = Collision2D.MouseClickedCollision(this, camera, new(size.Width, size.Height));
    if (collResult) {
      Logger.Info("COLL DETECTED");
    }
  }

  private Bounds2D GetBounds() {
    var pos = Owner!.GetComponent<RectTransform>().Position;
    var size = GetSize();

    _cachedBounds = new();
    _cachedBounds.Min = new Vector2(pos.X, pos.Y);
    _cachedBounds.Max = new Vector2(pos.X + size.X, pos.Y + size.Y);

    return _cachedBounds;
  }

  private Vector2 GetSize() {
    var scale = Owner!.GetComponent<RectTransform>().Scale;
    if (_lastKnownScale == scale) return _cachedSize;

    float minX, minY, maxX, maxY;

    maxX = _guiTexture.Mesh.Vertices[0].Position.X;
    maxY = _guiTexture.Mesh.Vertices[0].Position.Y;
    minX = _guiTexture.Mesh.Vertices[0].Position.X;
    minY = _guiTexture.Mesh.Vertices[0].Position.Y;

    for (int i = 0; i < _guiTexture.Mesh.Vertices.Length; i++) {
      if (minX > _guiTexture.Mesh.Vertices[i].Position.X) minX = _guiTexture.Mesh.Vertices[i].Position.X;
      if (maxX < _guiTexture.Mesh.Vertices[i].Position.X) maxX = _guiTexture.Mesh.Vertices[i].Position.X;

      if (minY > _guiTexture.Mesh.Vertices[i].Position.Y) minY = _guiTexture.Mesh.Vertices[i].Position.Y;
      if (maxY < _guiTexture.Mesh.Vertices[i].Position.Y) maxY = _guiTexture.Mesh.Vertices[i].Position.Y;
    }

    _lastKnownScale = scale;

    _cachedSize = new Vector2(
      MathF.Abs(minX - maxX) * scale.X,
      MathF.Abs(minY - maxY) * scale.Y
    );

    return _cachedSize;
  }

  public void Bind(IntPtr commandBuffer) {
    throw new NotImplementedException();
  }

  public Task Bind(IntPtr commandBuffer, uint index = 0) {
    _guiTexture.Bind(commandBuffer, index);
    return Task.CompletedTask;
  }

  public void BindDescriptorSet(VkDescriptorSet textureSet, FrameInfo frameInfo, ref VkPipelineLayout pipelineLayout) {
    _guiTexture.BindDescriptorSet(textureSet, frameInfo, ref pipelineLayout);
  }

  public void Dispose() {
    _guiTexture.Dispose();
  }

  public Task Draw(IntPtr commandBuffer, uint index = 0, uint firstInstance = 0) {
    _guiTexture.Draw(commandBuffer, index);
    return Task.CompletedTask;
  }

  public void DrawText(string text) {
    throw new NotImplementedException();
  }

  public Guid GetTextureIdReference() {
    return _guiTexture.GetTextureIdReference();
  }

  public void Update() {
    _guiTexture.Update();
  }

  public bool IsUI => true;
  public Vector2 Size => GetSize();
  public Bounds2D Bounds => GetBounds();
}
