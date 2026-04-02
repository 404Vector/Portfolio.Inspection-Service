using System.Text.Json.Serialization;

namespace Core.Models;

/// <summary>
/// 웨이퍼 좌표계상의 2D 위치를 마이크로미터(µm) 단위로 나타내는 값 객체입니다.
/// 원점(0, 0)은 웨이퍼 중심 또는 별도 정의된 기준점을 따릅니다.
/// </summary>
public readonly struct WaferCoordinate : IEquatable<WaferCoordinate> {
  /// <summary>X축 위치 (µm). 양의 방향 = 오른쪽.</summary>
  public double Xum { get; }

  /// <summary>Y축 위치 (µm). 양의 방향 = 위쪽 (SEMI 표준 기준).</summary>
  public double Yum { get; }

  [JsonConstructor]
  public WaferCoordinate(double xum, double yum) {
    Xum = xum;
    Yum = yum;
  }

  /// <summary>원점 (0, 0)</summary>
  public static WaferCoordinate Origin => new(0.0, 0.0);

  /// <summary>두 좌표 사이의 유클리드 거리 (µm)</summary>
  public double DistanceTo(WaferCoordinate other) {
    double dx = Xum - other.Xum;
    double dy = Yum - other.Yum;
    return Math.Sqrt(dx * dx + dy * dy);
  }

  public bool Equals(WaferCoordinate other) =>
    Xum == other.Xum && Yum == other.Yum;

  public override bool Equals(object? obj) =>
    obj is WaferCoordinate other && Equals(other);

  public override int GetHashCode() => HashCode.Combine(Xum, Yum);

  public static bool operator ==(WaferCoordinate left, WaferCoordinate right) => left.Equals(right);
  public static bool operator !=(WaferCoordinate left, WaferCoordinate right) => !left.Equals(right);

  public override string ToString() => $"({Xum}, {Yum}) µm";
}
