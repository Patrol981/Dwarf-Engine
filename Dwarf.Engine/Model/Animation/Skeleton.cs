using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Rendering;
using SharpGLTF.Schema2;

namespace Dwarf.Model.Animation;

public class Skeleton {
  public const int ROOT_JOINT = 0;
  public const int NO_PARENT = -1;
  // NEW
  public bool IsAnimated = true;
  public Matrix4x4[] FinalJointMatrices = [];
  public Dictionary<int, int> GlobalNodeToJointIdx = [];
  public string Name;
  public JointData[] JointsData = [];

  public Transform ParentTransform = null!;

  // OLD
  public Node Node;
  public Joint[] Joints { get; set; } = [];

  public void Update() {
    if (!IsAnimated) {
      for (int jIndex = 0; jIndex < JointsData.Length; jIndex++) {
        FinalJointMatrices[jIndex] = Matrix4x4.Identity;
      }
    } else {
      Guizmos.Clear();

      for (int jIndex = 0; jIndex < JointsData.Length; jIndex++) {
        // FinalJointMatrices[jIndex] = JointsData[jIndex].GetDeformedBindMatrix();
        FinalJointMatrices[jIndex] = JointsData[jIndex].GetDeformedBindMatrix();
      }

      UpdateJoint(ROOT_JOINT);


      for (int jIndex = 0; jIndex < JointsData.Length; jIndex++) {
        FinalJointMatrices[jIndex] = FinalJointMatrices[jIndex] * JointsData[jIndex].InverseBindMatrix;

        Guizmos.AddCircular(
          (FinalJointMatrices[jIndex] * ParentTransform.Matrix4).Translation,
          new(0.09f, 0.09f, 0.09f),
          new(1, 0, 0)
        );
        Guizmos.AddCircular(
          (JointsData[jIndex].InverseBindMatrix * ParentTransform.Matrix4).Translation,
          new(0.09f, 0.09f, 0.09f),
          new(1, 1, 0)
        );
      }
    }
  }

  public void UpdateJoint(int jointIndex) {
    var parentJoint = JointsData[jointIndex].ParentJoint;
    if (parentJoint != NO_PARENT) {
      FinalJointMatrices[jointIndex] = FinalJointMatrices[parentJoint] * FinalJointMatrices[jointIndex];
    }

    var childCount = JointsData[jointIndex].Children.Length;
    for (int i = 0; i < childCount; i++) {
      var childJoint = JointsData[jointIndex].Children[i];
      UpdateJoint(childJoint);
    }
  }

  public void Traverse() {
    Logger.Info($"Skeleton: {Name}");
    string indent = "";
    Traverse(JointsData[0], indent + "  ");
  }

  public void Traverse(JointData jointData, string indent) {
    JointData parentJointData = default;

    if (jointData.ParentJoint != NO_PARENT) {
      parentJointData = JointsData[jointData.ParentJoint];
    }
    Logger.Info($"{indent}name: {jointData.Name}, parent: {parentJointData.Name}, chlidCount: {jointData.Children.Length}");
    for (int i = 0; i < jointData.Children.Length; ++i) {
      // Logger.Info($"  {indent}child: {JointsData[jointData.Children[i]].Name}, index: {jointData.Children[i]}");
    }
    for (int i = 0; i < jointData.Children.Length; ++i) {
      // Traverse(Joints[joint.Children[i].Id], indent + " ");
      Traverse(JointsData[jointData.Children[i]], indent + "    ");
    }

  }

  // OLD

  public void UpdateJoint2(Joint joint) {
    // var current = Joints[joint.Id];

    var parentJoint = joint.Parent;
    if (parentJoint != null) {
      FinalJointMatrices[joint.Id] = FinalJointMatrices[parentJoint.Id] * FinalJointMatrices[joint.Id];
    }

    for (int i = 0; i < joint.Children.Count; ++i) {
      UpdateJoint2(joint.Children[i]);
    }
  }

  public void UpdateJoint2(int idx) {
    var current = Joints[idx];

    var parentJoint = current.Parent;
    if (parentJoint != null) {
      FinalJointMatrices[current.Id] = FinalJointMatrices[parentJoint.Id] * FinalJointMatrices[current.Id];
    }

    for (int i = 0; i < current.Children.Count; ++i) {
      UpdateJoint2(Joints[current.Children[i].Id]);
    }
  }

  public void Update2() {
    if (!IsAnimated) {
      for (int jIndex = 0; jIndex < Joints.Length; ++jIndex) {
        FinalJointMatrices[jIndex] = Matrix4x4.Identity;
      }
    } else {
      for (int jIndex = 0; jIndex < Joints.Length; ++jIndex) {
        FinalJointMatrices[jIndex] = Joints[jIndex].GetDeformedBindMatrix();
      }

      // UpdateJoint(Joints[0]);
      UpdateJoint2(ROOT_JOINT);

      for (int jIndex = 0; jIndex < Joints.Length; ++jIndex) {
        FinalJointMatrices[jIndex] *= Joints[jIndex].InverseBindMatrix;
      }
    }
  }

  public void Traverse2() {
    Logger.Info($"Skeleton: {Node.Name}");
    string indent = "";
    Traverse2(Joints[0], indent + "  ");
  }

  public void Traverse2(Joint joint, string indent) {
    Logger.Info($"{indent}name: {joint.Name}, parent: {joint.Parent?.Name}, chlidCount: {joint.Children.Count}");
    for (int i = 0; i < joint.Children.Count; ++i) {
      Logger.Info($"{indent}child: {joint.Children[i].Name}, index: {joint.Children[i].Id}");
    }
    for (int i = 0; i < joint.Children.Count; ++i) {
      // Traverse(Joints[joint.Children[i].Id], indent + " ");
      Traverse2(joint.Children[i], indent + "  ");
    }
  }
  public void AddJoints(List<Joint> joints) {
    // Joints.AddRange(joints);
    for (int i = 0; i < joints.Count; i++) {
      // FinalJointMatrices.Add(Matrix4x4.Identity);
    }
  }

  public void AddJoint(Joint joint) {
    // Joints.Add(joint);
    // FinalJointMatrices.Add(Matrix4x4.Identity);
  }

  public void RemoveJoint(Joint joint) {
    // Joints.Remove(joint);
    // FinalJointMatrices.RemoveAt(FinalJointMatrices.Count - 1);
  }
}