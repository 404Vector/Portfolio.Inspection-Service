namespace Core.Models;

/// <summary>
/// 카메라 1 grab에 해당하는 단일 촬영 단위입니다.
/// Shot의 FOV 영역과 해당 영역에 겹치는 Die 목록을 포함합니다.
/// </summary>
public readonly struct ScanShot : IEquatable<ScanShot> {
  /// <summary>Sector 내 Shot 순서 인덱스 (0-based)</summary>
  public int Index { get; }

  /// <summary>Shot FOV 중심 좌표 (웨이퍼 좌표계, µm)</summary>
  public WaferCoordinate Center { get; }

  /// <summary>카메라 FOV 크기 (µm)</summary>
  public FovSize Fov { get; }

  /// <summary>
  /// 이 Shot FOV와 겹치는 Die 인덱스 목록.
  /// Die의 어느 부분이라도 FOV와 겹치면 포함됩니다.
  /// </summary>
  public IReadOnlyList<DieIndex> CoveredDies { get; }

  public ScanShot(int index, WaferCoordinate center, FovSize fov, IReadOnlyList<DieIndex> coveredDies) {
    Index       = index;
    Center      = center;
    Fov         = fov;
    CoveredDies = coveredDies;
  }

  /// <summary>FOV 좌측 경계 X (µm)</summary>
  public double LeftUm => Center.Xum - Fov.WidthUm / 2.0;

  /// <summary>FOV 우측 경계 X (µm)</summary>
  public double RightUm => Center.Xum + Fov.WidthUm / 2.0;

  /// <summary>FOV 하단 경계 Y (µm)</summary>
  public double BottomUm => Center.Yum - Fov.HeightUm / 2.0;

  /// <summary>FOV 상단 경계 Y (µm)</summary>
  public double TopUm => Center.Yum + Fov.HeightUm / 2.0;

  /// <summary>
  /// 주어진 DieRegion이 이 Shot FOV와 겹치는지 판정합니다 (AABB 교차 검사).
  /// Die의 어느 부분이라도 FOV와 겹치면 true를 반환합니다.
  /// </summary>
  public bool Overlaps(DieRegion die) {
    var tr = die.TopRight;
    var bl = die.BottomLeft;
    return bl.Xum < RightUm  &&
           tr.Xum > LeftUm   &&
           bl.Yum < TopUm    &&
           tr.Yum > BottomUm;
  }

  public bool Equals(ScanShot other) => Index == other.Index && Center == other.Center;

  public override bool Equals(object? obj) => obj is ScanShot other && Equals(other);

  public override int GetHashCode() => HashCode.Combine(Index, Center);

  public static bool operator ==(ScanShot left, ScanShot right) => left.Equals(right);
  public static bool operator !=(ScanShot left, ScanShot right) => !left.Equals(right);

  public override string ToString() => $"Shot[{Index}] Center={Center} CoveredDies={CoveredDies.Count}";
}
