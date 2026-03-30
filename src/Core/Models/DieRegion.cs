namespace Core.Models;

/// <summary>
/// 웨이퍼 좌표계(µm)에서 Die 하나의 식별자와 물리적 영역을 나타내는 값 객체입니다.
/// 영역은 축 정렬 사각형(AABB)으로 표현됩니다.
/// </summary>
public readonly struct DieRegion : IEquatable<DieRegion> {
  /// <summary>Die의 격자 인덱스 식별자</summary>
  public DieIndex Index { get; }

  /// <summary>Die 좌하단 꼭짓점 (µm). 웨이퍼 좌표계 기준.</summary>
  public WaferCoordinate BottomLeft { get; }

  /// <summary>Die 너비 (µm)</summary>
  public double WidthUm { get; }

  /// <summary>Die 높이 (µm)</summary>
  public double HeightUm { get; }

  public DieRegion(DieIndex index, WaferCoordinate bottomLeft, double widthUm, double heightUm) {
    Index      = index;
    BottomLeft = bottomLeft;
    WidthUm    = widthUm;
    HeightUm   = heightUm;
  }

  /// <summary>Die 우상단 꼭짓점 (µm)</summary>
  public WaferCoordinate TopRight =>
    new(BottomLeft.Xum + WidthUm, BottomLeft.Yum + HeightUm);

  /// <summary>Die 중심 좌표 (µm)</summary>
  public WaferCoordinate Center =>
    new(BottomLeft.Xum + WidthUm / 2.0, BottomLeft.Yum + HeightUm / 2.0);

  /// <summary>주어진 좌표가 Die 영역 내에 있는지 확인합니다 (경계 포함).</summary>
  public bool Contains(WaferCoordinate point) =>
    point.Xum >= BottomLeft.Xum && point.Xum <= BottomLeft.Xum + WidthUm &&
    point.Yum >= BottomLeft.Yum && point.Yum <= BottomLeft.Yum + HeightUm;

  public bool Equals(DieRegion other) => Index == other.Index;

  public override bool Equals(object? obj) => obj is DieRegion other && Equals(other);

  public override int GetHashCode() => Index.GetHashCode();

  public static bool operator ==(DieRegion left, DieRegion right) => left.Equals(right);
  public static bool operator !=(DieRegion left, DieRegion right) => !left.Equals(right);

  public override string ToString() =>
    $"{Index} BL={BottomLeft} {WidthUm}x{HeightUm}µm";
}
