using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Globals;
using Dwarf.Engine.Math;
using Dwarf.Extensions.Logging;

using JoltPhysicsSharp;

using OpenTK.Mathematics;

using static Dwarf.Engine.Physics.JoltConfig;

namespace Dwarf.Engine.Physics;
public class Rigidbody : Component, IDisposable {
  private BodyID _bodyId;

  public Rigidbody() { }

  public void Init(in BodyInterface bodyInterface) {
    // BodyCreationSettings settings = new(new BoxShape())
    var pos = Translator.OpenTKToSystemNumericsVector(Owner!.GetComponent<Transform>().Position);
    BodyCreationSettings settings = new(
      new SphereShape(0.5f),
      pos,
      System.Numerics.Quaternion.Identity,
      MotionType.Dynamic,
      Layers.Moving
    );
    _bodyId = bodyInterface.CreateAndAddBody(settings, Activation.Activate);
    var vec3 = new Vector3(0.0f, -0.1f, 0.0f);
    bodyInterface.SetLinearVelocity(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    // bodyInterface.MoveKinematic(_bodyId, new(20, -20, 0), System.Numerics.Quaternion.Identity, Time.DeltaTime);
  }

  public void Update(in BodyInterface bodyInterface) {

    var pos = bodyInterface.GetPosition(_bodyId);
    Owner!.GetComponent<Transform>().Position.X = (float)pos.X;
    Owner!.GetComponent<Transform>().Position.Y = (float)pos.Y;
    Owner!.GetComponent<Transform>().Position.Z = (float)pos.Z;
    Logger.Warn($"[P POS] {pos.ToString()}");
    // bodyInterface.MoveKinematic(_bodyId, )
    // Owner.GetComponent<Transform>().Position = _bodyId.
  }

  public void Dispose() {

  }
}
