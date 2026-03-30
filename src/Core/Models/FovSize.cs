namespace Core.Models;

/// <summary>
/// 카메라의 Field of View(시야) 크기를 마이크로미터(µm) 단위로 나타내는 값 객체입니다.
/// DieSize와 의미적으로 구별됩니다 — FovSize는 광학계 스펙, DieSize는 공정 레이아웃 스펙입니다.
/// </summary>
public readonly struct FovSize : IEquatable<FovSize> {
  /// <summary>FOV 너비 (µm). X축 방향.</summary>
  public double WidthUm { get; }

  /// <summary>FOV 높이 (µm). Y축 방향.</summary>
  public double HeightUm { get; }

  public FovSize(double widthUm, double heightUm) {
    if (widthUm <= 0) throw new ArgumentOutOfRangeException(nameof(widthUm), "FOV 너비는 0보다 커야 합니다.");
    if (heightUm <= 0) throw new ArgumentOutOfRangeException(nameof(heightUm), "FOV 높이는 0보다 커야 합니다.");
    WidthUm  = widthUm;
    HeightUm = heightUm;
  }

  public bool Equals(FovSize other) =>
    WidthUm == other.WidthUm && HeightUm == other.HeightUm;

  public override bool Equals(object? obj) => obj is FovSize other && Equals(other);

  public override int GetHashCode() => HashCode.Combine(WidthUm, HeightUm);

  public static bool operator ==(FovSize left, FovSize right) => left.Equals(right);
  public static bool operator !=(FovSize left, FovSize right) => !left.Equals(right);

  public override string ToString() => $"{WidthUm} x {HeightUm} µm";
}
