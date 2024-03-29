using System.Numerics;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Loaders;
using Dwarf.Engine.Physics;
using Dwarf.Extensions.Logging;

namespace Dwarf.Engine.Testing;
public class PerformanceTester {
  public static void KeyHandler(int action, int key) {
    switch (action) {
      case (int)Dwarf.KeyAction.GLFW_PRESS:
        if (key == (int)Dwarf.Keys.GLFW_KEY_P) CreateNewModel(Application.Instance, false);
        if (key == (int)Dwarf.Keys.GLFW_KEY_LEFT_BRACKET) CreateNewModel(Application.Instance, true);
        if (key == (int)Dwarf.Keys.GLFW_KEY_O) RemoveModel(Application.Instance);

        if (key == (int)Dwarf.Keys.GLFW_KEY_L) CreateNewComlexModel(Application.Instance, false);
        if (key == (int)Dwarf.Keys.GLFW_KEY_K) CreateNewComlexModel(Application.Instance, true);
        if (key == (int)Dwarf.Keys.GLFW_KEY_J) RemoveComplexModel(Application.Instance);
        break;
    }
  }

  public static async void CreateNewModel(Application app, bool addTexture = false) {
    if (!addTexture) return; ;

    var entity = await EntityCreator.Create3DPrimitive(
      "test",
      "./Resources/gigachad.png",
      PrimitiveType.Cylinder,
      new(-5, 0, 0),
      new(0, 0, 0),
      new(1, 1, 1)
    );
    entity.AddRigdbody(PrimitiveType.Cylinder, false, 1);
    entity.GetComponent<Rigidbody>().Init(Application.Instance.GetSystems().PhysicsSystem.BodyInterface);
    app.AddEntity(entity);
  }

  public static async void CreateNewComlexModel(Application app, bool addTexture = false) {

    if (addTexture) {
      var prefix = "./Resources/Textures/";
      string[] anime1Paths = {
        $"{prefix}dwarf_test_model/_01.png", // mouth
        $"{prefix}dwarf_test_model/_02.png", // eyes
        $"{prefix}dwarf_test_model/_03.png", // eye mid
        $"{prefix}dwarf_test_model/_04.png", // face
        $"{prefix}dwarf_test_model/_06.png", // possibly face shadow ?
        $"{prefix}dwarf_test_model/_07.png", // eyebrows
        $"{prefix}dwarf_test_model/_08.png", // eyeleashes
        $"{prefix}dwarf_test_model/_09.png", // eyeleashes
        $"{prefix}dwarf_test_model/_10.png", // body
        $"{prefix}dwarf_test_model/_12.png", // hair base
        $"{prefix}dwarf_test_model/_13.png", // outfit
        $"{prefix}dwarf_test_model/_14.png", // outfit
        $"{prefix}dwarf_test_model/_15.png", // outfit
        $"{prefix}dwarf_test_model/_16.png", // outfit
        $"{prefix}dwarf_test_model/_17.png", // outfit
        $"{prefix}dwarf_test_model/_18.png", // outfit
        $"{prefix}dwarf_test_model/_19.png", // hair
      };

      List<List<string>> paths = new() {
        anime1Paths.ToList()
      };
      // vkQueueWaitIdle(app.Device.GraphicsQueue);
      // vkDeviceWaitIdle(app.Device.LogicalDevice);
      await app.LoadTexturesAsSeparateThreads(paths);
    }
    string[] texturesToLoad = {
      "dwarf_test_model/_01.png", // mouth
      "dwarf_test_model/_02.png", // eyes
      "dwarf_test_model/_03.png", // eye mid
      "dwarf_test_model/_04.png", // face
      "dwarf_test_model/_06.png", // possibly face shadow ?
      "dwarf_test_model/_07.png", // eyebrows
      "dwarf_test_model/_08.png", // eyeleashes
      "dwarf_test_model/_09.png", // eyeleashes
      "dwarf_test_model/_10.png", // body
      "dwarf_test_model/_12.png", // hair base
      "dwarf_test_model/_13.png", // outfit
      "dwarf_test_model/_14.png", // outfit
      "dwarf_test_model/_15.png", // outfit
      "dwarf_test_model/_16.png", // outfit
      "dwarf_test_model/_17.png", // outfit
      "dwarf_test_model/_18.png", // outfit
      "dwarf_test_model/_19.png", // hair
    };

    var en = new Entity();
    var startModelTime = DateTime.Now;
    var model = await new GenericLoader().LoadModelOptimized(app.Device, app.Renderer, "./Textures/Models/dwarf_test_model.obj");
    en.AddComponent(model);
    var endModelTime = DateTime.Now;
    en.GetComponent<MeshRenderer>().BindMultipleModelPartsToTextures(app.TextureManager, texturesToLoad);
    en.AddComponent(new Material(new Vector3(1f, 0.7f, 0.9f)));
    en.AddComponent(new Transform(new Vector3(0.0f, 0f, 0f)));
    en.GetComponent<Transform>().Scale = new(1f, 1f, 1f);
    en.GetComponent<Transform>().Rotation = new(180f, 0f, 0);
    en.AddComponent(new Rigidbody(app.Device, PrimitiveType.Cylinder, 0.25f));
    var bodyInterface = Application.Instance.GetSystems().PhysicsSystem.BodyInterface;
    en.GetComponent<Rigidbody>().Init(bodyInterface);
    en.Name = "ComplexTest";
    app.AddEntity(en);

    Logger.Warn($"[CREATE ENTITY TIME]: {(endModelTime - startModelTime).TotalMilliseconds}");
  }

  public static void RemoveModel(Application app) {
    var room = app.GetEntities().Where(x => x.Name == "test").FirstOrDefault();
    if (room == null) return;
    room.CanBeDisposed = true;
  }

  public static void RemoveComplexModel(Application app) {
    var en = app.GetEntities().Where(x => x.Name == "ComplexTest").FirstOrDefault();
    if (en == null) return;
    en.CanBeDisposed = true;
  }
}
