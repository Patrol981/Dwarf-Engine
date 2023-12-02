using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Math;
using Dwarf.Engine.Physics;
using Dwarf.Extensions.GLFW;
using Dwarf.Extensions.Logging;

using System.Numerics;
namespace Dwarf.Engine.Globals;

public sealed class MouseState {
  private static MouseState s_instance = null!;

  public event EventHandler ClickEvent;

  private OpenTK.Mathematics.Vector2d _lastMousePositionFromCallback = new(0, 0);
  private double _previousScrollY = 0.0;
  private double _scrollDelta = 0.0;

  private System.Numerics.Vector3 _selectedColor = new(1, 0, 0);

  public unsafe static void MouseCallback(GLFWwindow* window, double xpos, double ypos) {
    MouseState.GetInstance()._lastMousePositionFromCallback = new(xpos, ypos);
  }

  public unsafe static void ScrollCallback(GLFWwindow* window, double xoffset, double yoffset) {
    double currentScrollY = yoffset;
    MouseState.GetInstance()._scrollDelta = currentScrollY += yoffset;
    MouseState.GetInstance()._previousScrollY = currentScrollY;
  }

  public unsafe static void MouseButtonCallback(GLFWwindow* window, int button, int action, int mods) {
    if (action == 1) {
      MouseState.GetInstance().OnClicked(null!);
    }
  }

  private void OnClicked(EventArgs e) {
    ClickEvent?.Invoke(this, e);

    //  var entities = ApplicationState.Instance.GetEntities();
    // var models = Entity.Distinct<Model>(entities);

    /*
    foreach (var model in models) {
      model.GetComponent<Material>().SetColor(new(1, 1, 1));
    }

    var camera = CameraState.GetCamera();
    var screenSize = ApplicationState.Instance.Window.Extent;
    var rayInfo = Ray.GetRayInfo(camera, new(screenSize.width, screenSize.height));

    var target = entities.Where(x => x.Name == "NPC").First();
    var target2 = entities.Where(x => x.Name == "NPC2").First();
    // target.GetComponent<Transform>().Position = rayInfo.RayDirectionRaw;
    // target2.GetComponent<Transform>().Position = rayInfo.RayOrigin;

    foreach (var model in models) {
      // var result = Ray.OBBIntersection(model, 5000);
      var result = Ray.OBBIntersection_Base(model, 5000);
      // var result = Ray.MeshIntersection(model);
      if (result.Present) {
        model.GetComponent<Material>().SetColor(_selectedColor);
        break;
      }
    }
    */
  }

  public OpenTK.Mathematics.Vector2d MousePosition => _lastMousePositionFromCallback;
  public double ScrollDelta {
    get { return _scrollDelta; }
    set { _scrollDelta = value; }
  }
  public double PreviousScroll => _previousScrollY;

  public static MouseState GetInstance() {
    if (s_instance == null) {
      s_instance = new MouseState();
    }
    return s_instance;
  }
}