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
  private BodyInterface _bodyInterface;

  private BodyID _bodyId;
  private MotionType _motionType = MotionType.Dynamic;
  private MotionQuality _motionQuality = MotionQuality.LinearCast;

  public Rigidbody() { }

  public unsafe void Init(in BodyInterface bodyInterface) {
    _bodyInterface = bodyInterface;

    // BodyCreationSettings settings = new(new BoxShape())
    var pos = Translator.OpenTKToSystemNumericsVector(Owner!.GetComponent<Transform>().Position);
    BodyCreationSettings settings = new(
      new SphereShape(0.5f),
      pos,
      System.Numerics.Quaternion.Identity,
      _motionType,
      Layers.Moving
    );

    var model = Owner!.GetComponent<Model>();
    List<System.Numerics.Vector3> verts = new();

    foreach (var m in model.Meshes) {
      foreach (var v in m.Vertices) {
        verts.Add(Translator.OpenTKToSystemNumericsVector(v.Position));
      }
    }

    BodyCreationSettings convex;

    var vArray = verts.ToArray();
    fixed (System.Numerics.Vector3* ptr = vArray) {
      var convexHull = new ConvexHullShapeSettings(ptr, vArray.Length);
      convex = new(
        convexHull,
        pos,
        System.Numerics.Quaternion.Identity,
        _motionType,
        Layers.Moving
      );
    }



    _bodyId = _bodyInterface.CreateAndAddBody(settings, Activation.Activate);
    var vec3 = new Vector3(0.0f, 1.0f, 0.0f);
    _bodyInterface.SetGravityFactor(_bodyId, 0.025f);
    _bodyInterface.SetMotionQuality(_bodyId, _motionQuality);
    // bodyInterface.SetLinearVelocity(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    // bodyInterface.MoveKinematic(_bodyId, new(20, -20, 0), System.Numerics.Quaternion.Identity, Time.DeltaTime);
  }

  public void Update() {

    var pos = _bodyInterface.GetCenterOfMassPosition(_bodyId);
    var vec3 = new OpenTK.Mathematics.Vector3((float)pos.X, (float)pos.Y, (float)pos.Z);
    Owner!.GetComponent<Transform>().Position = vec3;
    // bodyInterface.MoveKinematic(_bodyId, )
    // Owner.GetComponent<Transform>().Position = _bodyId.
    // Logger.Info($"[ACTIVE] {_bodyInterface.IsActive(_bodyId)}");
    // _bodyInterface.ActivateBody(_bodyId);
  }

  public void AddForce(OpenTK.Mathematics.Vector3 vec3) {
    _bodyInterface.AddForce(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    _bodyInterface.ActivateBody(_bodyId);
  }

  public void AddVelocity(OpenTK.Mathematics.Vector3 vec3) {
    _bodyInterface.AddLinearVelocity(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    _bodyInterface.ActivateBody(_bodyId);
  }

  public void AddImpulse(OpenTK.Mathematics.Vector3 vec3) {
    _bodyInterface.AddImpulse(_bodyId, Translator.OpenTKToSystemNumericsVector(vec3));
    _bodyInterface.ActivateBody(_bodyId);
  }

  public void Translate(OpenTK.Mathematics.Vector3 vec3) {
    var pos = _bodyInterface.GetPosition(_bodyId);
    pos.X += vec3.X;
    pos.Y += vec3.Y;
    pos.Z += vec3.Z;
    _bodyInterface.SetPosition(_bodyId, pos, Activation.Activate);
  }

  public void Dispose() {
    _bodyInterface.DeactivateBody(_bodyId);
    _bodyInterface.DestroyBody(_bodyId);
  }
}
