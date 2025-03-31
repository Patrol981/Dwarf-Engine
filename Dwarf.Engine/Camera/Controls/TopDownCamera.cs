using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Windowing;

namespace Dwarf;

public class TopDownCamera : DwarfScript {
  public override void Update() {
    MoveByPC();
  }
  public unsafe void MoveByPC() {
    if (Input.GetKey(Scancode.D)) {
      Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.A)) {
      Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.W)) {
      Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.S)) {
      Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }

    if (Input.GetKey(Scancode.E)) {
      Owner!.GetComponent<Transform>().Position -= Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.Q)) {
      Owner!.GetComponent<Transform>().Position += Owner!.GetComponent<Camera>().Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
  }
}