using System.Text.Json.Serialization;

namespace Core.Models;

/// <summary>
/// Die 하나의 물리적 크기를 마이크로미터(µm) 단위로 나타내는 값 객체입니다.
/// </summary>
public readonly struct DieSize : IEquatable<DieSize> {
  /// <summary>Die 너비 (µm)</summary>
  public double WidthUm { get; }

  /// <summary>Die 높이 (µm)</summary>
  public double HeightUm { get; }

  [JsonConstructor]
  public DieSize(double widthUm, double heightUm) {
    if (widthUm <= 0) throw new ArgumentOutOfRangeException(nameof(widthUm), "Die 너비는 0보다 커야 합니다.");
    if (heightUm <= 0) throw new ArgumentOutOfRangeException(nameof(heightUm), "Die 높이는 0보다 커야 합니다.");
    WidthUm = widthUm;
    HeightUm = heightUm;
  }

  public bool Equals(DieSize other) =>
    WidthUm == other.WidthUm && HeightUm == other.HeightUm;

  public override bool Equals(object? obj) =>
    obj is DieSize other && Equals(other);

  public override int GetHashCode() => HashCode.Combine(WidthUm, HeightUm);

  public static bool operator ==(DieSize left, DieSize right) => left.Equals(right);
  public static bool operator !=(DieSize left, DieSize right) => !left.Equals(right);

  public override string ToString() => $"{WidthUm} x {HeightUm} µm";
}
