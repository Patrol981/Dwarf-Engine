using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Loaders;
using Dwarf.Math;
using Dwarf.Rendering;
using glTFLoader;
using glTFLoader.Schema;

namespace Dwarf.Model.Animation;

public class Skeleton {
  public const int ROOT_JOINT = 0;
  public const int NO_PARENT = -1;
  // NEW
  public bool IsAnimated = true;
  public Matrix4x4[] FinalJointMatrices = [];
  public Dictionary<int, int> GlobalNodeToJointIdx = [];
  public string Name = string.Empty;
  public Joint[] Joints = [];
  public Node MeshNode { get; private set; } = null!;

  public Transform ParentTransform = null!;

  public Skeleton(Node meshNode) {
    MeshNode = meshNode;
  }

  public void Update() {
    if (!IsAnimated) {
      // Logger.Info($"Applying identity to {Name}");
      for (int jIndex = 0; jIndex < Joints.Length; jIndex++) {
        FinalJointMatrices[jIndex] = Matrix4x4.Identity;
      }
    } else {
      Guizmos.Clear();
      // Matrix4x4.Invert(GLTFLoaderKHR.GetNodeMatrix(Joints[0].Node), out var inMat);
      for (int jointIdx = 0; jointIdx < Joints.Length; jointIdx++) {
        FinalJointMatrices[jointIdx] = Joints[jointIdx].GetDeformedBindMatrix();
        // Matrix4x4.Invert(Joints[jIndex].GetDeformedBindMatrix(), out var inMat);
        // FinalJointMatrices[jIndex] = inMat;
        // var jointMat = Joints[jointIdx].GetDeformedBindMatrix() * Joints[jointIdx].InverseBindMatrix;
        // jointMat = jointMat * inMat;
        // FinalJointMatrices[jointIdx] = jointMat;
        // FinalJointMatrices[jointIdx] = inMat * matrix * Joints[jointIdx].GetDeformedBindMatrix() * Joints[jointIdx].InverseBindMatrix;
      }

      UpdateJoint(ROOT_JOINT);


      for (int jIndex = 0; jIndex < Joints.Length; jIndex++) {
        FinalJointMatrices[jIndex] = FinalJointMatrices[jIndex] * Joints[jIndex].InverseBindMatrix;
        // FinalJointMatrices[jIndex] = matrix * FinalJointMatrices[jIndex];

        Guizmos.AddCircular(
          (FinalJointMatrices[jIndex] * ParentTransform.ScaleMatrix).Translation,
          new(0.1f, 0.1f, 0.1f),
          new(1, 0, 0)
        );
        Guizmos.AddCircular(
          (ParentTransform.Matrix4 * Joints[jIndex].InverseBindMatrix).Translation,
          new(0.5f, 0.5f, 0.5f),
          new(1, 1, 0)
        );
      }
    }
  }

  public void UpdateJoint(int jointIndex) {
    var parentJoint = Joints[jointIndex].ParentJoint;
    if (parentJoint != NO_PARENT) {
      FinalJointMatrices[jointIndex] = FinalJointMatrices[parentJoint] * FinalJointMatrices[jointIndex];
    }

    var childCount = Joints[jointIndex].Children.Length;
    for (int i = 0; i < childCount; i++) {
      var childJoint = Joints[jointIndex].Children[i];
      UpdateJoint(childJoint);
    }
  }

  public void Traverse() {
    Logger.Info($"Skeleton: {Name}");
    string indent = "";
    Traverse(Joints[0], indent + "  ");
  }

  public void Traverse(Joint joint, string indent) {
    Joint parentJointData = default;

    if (joint.ParentJoint != NO_PARENT) {
      parentJointData = Joints[joint.ParentJoint];
    }
    Logger.Info($"{indent}name: {joint.Name}, parent: {parentJointData.Name}, chlidCount: {joint.Children.Length}");
    for (int i = 0; i < joint.Children.Length; ++i) {
      // Logger.Info($"  {indent}child: {JointsData[jointData.Children[i]].Name}, index: {jointData.Children[i]}");
    }
    for (int i = 0; i < joint.Children.Length; ++i) {
      // Traverse(Joints[joint.Children[i].Id], indent + " ");
      Traverse(Joints[joint.Children[i]], indent + "    ");
    }

  }
}