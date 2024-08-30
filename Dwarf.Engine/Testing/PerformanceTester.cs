using Dwarf.EntityComponentSystem;
using Dwarf.Physics;

namespace Dwarf.Testing;
public class PerformanceTester {
  public static void KeyHandler(int action, int key) {
    switch (action) {
      case (int)Dwarf.KeyAction.GLFW_PRESS:
        if (key == (int)Dwarf.Keys.GLFW_KEY_P) CreateNewModel(Application.Instance, false);
        if (key == (int)Dwarf.Keys.GLFW_KEY_LEFT_BRACKET) CreateNewModel(Application.Instance, true);
        if (key == (int)Dwarf.Keys.GLFW_KEY_O) RemoveModel(Application.Instance);
        break;
    }
  }

  public static Task CreateNewModel(Application app, bool addTexture = false) {
    if (!addTexture) return Task.CompletedTask;

    var entity = new Entity {
      Name = "test"
    };
    entity.AddTransform(new(-5, 0, 0), new(90, 0, 0));
    entity.AddMaterial();
    entity.AddPrimitive("./Resources/gigachad.png", PrimitiveType.Cylinder);
    // entity.AddModel("./Resources/tks.glb");
    // entity.AddRigdbody(PrimitiveType.Cylinder, false, 1);
    // entity.GetComponent<Rigidbody>().Init(Application.Instance.Systems.PhysicsSystem.BodyInterface);
    app.AddEntity(entity);
    return Task.CompletedTask;
  }

  public static void RemoveModel(Application app) {
    var room = app.GetEntities().Where(x => x.Name == "test").FirstOrDefault();
    if (room == null) return;
    room.CanBeDisposed = true;
  }
}
