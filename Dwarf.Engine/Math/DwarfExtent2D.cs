using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;

using Vortice.Vulkan;

namespace Dwarf.Math;

public static class DwarfExtentExtensions {
  public static DwarfExtent2D FromVkExtent2D(this VkExtent2D vkExtent2D) {
    return new DwarfExtent2D(vkExtent2D.width, vkExtent2D.height);
  }

  public static VkExtent2D FromDwarfExtent2D(this DwarfExtent2D dwarfExtent2D) {
    return new VkExtent2D(dwarfExtent2D.Width, dwarfExtent2D.Height);
  }
}

[StructLayout(LayoutKind.Sequential)]
public struct DwarfExtent2D : IEquatable<DwarfExtent2D> {
  public uint Width;
  public uint Height;

  public DwarfExtent2D(uint width, uint height) {
    Width = width;
    Height = height;
  }

  public DwarfExtent2D(int width, int height) {
    Width = (uint)width;
    Height = (uint)height;
  }

  /// <inheritdoc/>
  public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is DwarfExtent2D other && Equals(other);

  /// <inheritdoc/>
  public readonly bool Equals(DwarfExtent2D other) => Width == other.Width && Height == other.Height;

  /// <inheritdoc/>
  public override readonly int GetHashCode() => HashCode.Combine(Width, Height);

  /// <inheritdoc/>
  public override readonly string ToString() => $"{{Width={Width},Height={Height}}}";

  /// <summary>
  /// Compares two <see cref="DwarfExtent2D"/> objects for equality.
  /// </summary>
  /// <param name="left">The <see cref="DwarfExtent2D"/> on the left hand of the operand.</param>
  /// <param name="right">The <see cref="DwarfExtent2D"/> on the right hand of the operand.</param>
  /// <returns>
  /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
  /// </returns>
  public static bool operator ==(DwarfExtent2D left, DwarfExtent2D right) => left.Equals(right);

  /// <summary>
  /// Compares two <see cref="DwarfExtent2D"/> objects for inequality.
  /// </summary>
  /// <param name="left">The <see cref="DwarfExtent2D"/> on the left hand of the operand.</param>
  /// <param name="right">The <see cref="DwarfExtent2D"/> on the right hand of the operand.</param>
  /// <returns>
  /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
  /// </returns>
  public static bool operator !=(DwarfExtent2D left, DwarfExtent2D right) => !left.Equals(right);

  /// <summary>
  /// Performs an implicit conversion from <see cre ="VkExtent2D"/> to <see cref="Size" />.
  /// </summary>
  /// <param name="value">The value to convert.</param>
  /// <returns>The result of the conversion.</returns>
  public static implicit operator Size(DwarfExtent2D value) => new((int)value.Width, (int)value.Height);

  /// <summary>
  /// Performs an implicit conversion from <see cre ="Size"/> to <see cref="DwarfExtent2D" />.
  /// </summary>
  /// <param name="value">The value to convert.</param>
  /// <returns>The result of the conversion.</returns>
  public static implicit operator DwarfExtent2D(Size value) => new(value.Width, value.Height);

  public VkExtent2D ToVkExtent2D() {
    return new VkExtent2D(Width, Height);
  }

  public static DwarfExtent2D Zero => default;
}
