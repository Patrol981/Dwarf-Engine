using System.Numerics;
using JoltPhysicsSharp;
using glTFLoader.Schema;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Loaders;

namespace Dwarf.Model.Animation;

public struct Joint {
  public string Name = default!;
  public Matrix4x4 InverseBindMatrix = Matrix4x4.Identity;

  public Vector3 DeformedNodeTranslation = Vector3.Zero;
  public Quaternion DeformedNodeRotation = Quaternion.Identity;
  public Vector3 DeformedNodeScale = Vector3.One;

  public int ParentJoint;
  public int[] Children = [];
  public Node Node = null!;

  public Joint() {
  }

  /*
  public Matrix4x4 GetLocalMatrix() {
    var translation = Node.Translation.ToVector3();
    var rotation = Node.Rotation.ToQuat();
    var scale = Node.Scale.ToVector3();

    var translationMatrix = Matrix4x4.Identity * Matrix4x4.CreateTranslation(translation);
    var rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
    var scaleMatrix = Matrix4x4.Identity * Matrix4x4.CreateScale(scale);

    return translationMatrix * rotationMatrix * scaleMatrix;
  }
  */

  public Matrix4x4 GetDeformedBindMatrix() {
    var translation = Matrix4x4.CreateTranslation(DeformedNodeTranslation);
    var scale = Matrix4x4.CreateScale(DeformedNodeScale);
    var rotation = Matrix4x4.CreateFromQuaternion(DeformedNodeRotation);
    var angleX = Converter.DegreesToRadians(DeformedNodeRotation.X);
    var angleY = Converter.DegreesToRadians(DeformedNodeRotation.Y);
    var angleZ = Converter.DegreesToRadians(DeformedNodeRotation.Z);
    var rot = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);

    // Logger.Info($"{Name} : {DeformedNodeTranslation}");

    var mat = translation * scale;
    mat = Matrix4x4.Transform(mat, DeformedNodeRotation);
    // return Matrix4x4.Transpose(scale * rotation * translation);

    // Typically, the order of transformations is scale -> rotation -> translation
    // return Matrix4x4.Transpose(scale * rotation * translation);
    return translation * rotation * scale;
    // return Matrix4x4.Transpose(translation) * Matrix4x4.Transpose(rotation) * Matrix4x4.Transpose(scale);

    return new Matrix4x4(
      DeformedNodeTranslation.X, DeformedNodeTranslation.Y, DeformedNodeTranslation.Z, 0,
      DeformedNodeRotation.X, DeformedNodeRotation.Y, DeformedNodeRotation.Z, 0,
      DeformedNodeScale.X, DeformedNodeScale.Y, DeformedNodeScale.Z, 0,
      0, 0, 0, 1
    );
    // return scale * rot * translation;
    //
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