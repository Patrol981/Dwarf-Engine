namespace Dwarf.Math;
public static class Clamp {
  public static float ClampToClosestAngle(float value) {
    // Define the possible angles
    float[] angles = [0, 90, 180, 270];

    // Initialize the closest angle to the first element
    var closestAngle = angles[0];
    var smallestDifference = MathF.Abs(value - closestAngle);

    // Iterate through the angles to find the closest one
    foreach (var angle in angles) {
      var difference = MathF.Abs(value - angle);
      if (difference < smallestDifference) {
        smallestDifference = difference;
        closestAngle = angle;
      }
    }

    return closestAngle;
  }
}
