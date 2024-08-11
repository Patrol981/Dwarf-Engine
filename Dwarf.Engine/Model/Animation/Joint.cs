using System.Numerics;
using JoltPhysicsSharp;
using glTFLoader.Schema;
using Dwarf.Extensions.Logging;
using Dwarf.Math;

namespace Dwarf.Model.Animation;

public struct JointData {
  public string Name = default!;
  public Matrix4x4 InverseBindMatrix = Matrix4x4.Identity;

  public Vector3 DeformedNodeTranslation = Vector3.Zero;
  public Quaternion DeformedNodeRotation = Quaternion.Identity;
  public Vector3 DeformedNodeScale = Vector3.One;

  public int ParentJoint;
  public int[] Children = [];
  public Node Node = null!;

  public JointData() {
  }

  public Matrix4x4 GetDeformedBindMatrix() {
    var translation = Matrix4x4.CreateTranslation(DeformedNodeTranslation);
    var scale = Matrix4x4.CreateScale(DeformedNodeScale);
    var rotation = Matrix4x4.CreateFromQuaternion(DeformedNodeRotation);
    var angleX = Converter.DegreesToRadians(DeformedNodeRotation.X);
    var angleY = Converter.DegreesToRadians(DeformedNodeRotation.Y);
    var angleZ = Converter.DegreesToRadians(DeformedNodeRotation.Z);
    var rot = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);

    // Logger.Info($"{Name} : {DeformedNodeTranslation}");

    // Typically, the order of transformations is scale -> rotation -> translation
    return translation * rot * scale;
    // return scale * rotation * translation;
    // return scale * rot * translation;
    // return translation * rot * scale;
    // return Node.LocalMatrix * Node.LocalTransform.Matrix;
  }

  public Matrix4x4 GetDeformedBindMatrix(Matrix4x4 transform) {
    var translation = Matrix4x4.CreateTranslation(DeformedNodeTranslation);
    var scale = Matrix4x4.CreateScale(DeformedNodeScale);
    var rotation = Matrix4x4.CreateFromQuaternion(DeformedNodeRotation);

    return (scale * rotation * translation) * transform;
  }
}

public class Joint {
  public Node JointNode { get; private set; }
  public int Id;
  public string Name;
  public Joint? Parent;
  public List<Joint> Children = [];

  public Matrix4x4 UndeformedNodeMatrix = Matrix4x4.Identity;
  public Matrix4x4 InverseBindMatrix; // undeformed inverse node matrix

  public Vector3 DeformedNodeTranslation = Vector3.Zero;
  public Quaternion DeformedNodeRotation = Quaternion.Identity;
  public Vector3 DeformedNodeScale = Vector3.One;

  public Joint(Node node, int id) {
    JointNode = node;
    Id = id;
    Name = node.Name;

    /*
    foreach (var child in JointNode.VisualChildren) {
      var joint = new Joint(child, child.LogicalIndex);
      Children.Add(joint);
    }
    */
  }

  public Matrix4x4 GetDeformedBindMatrix() {
    var translation = Matrix4x4.CreateTranslation(DeformedNodeTranslation);
    var scale = Matrix4x4.CreateScale(DeformedNodeScale);
    return translation * Matrix4x4.CreateFromQuaternion(DeformedNodeRotation) * scale;
  }
}