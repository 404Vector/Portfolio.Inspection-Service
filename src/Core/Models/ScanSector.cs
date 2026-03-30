using Core.Enums;

namespace Core.Models;

/// <summary>
/// Boustrophedon 경로에서 가로 1행에 해당하는 스캔 단위입니다.
/// KLA 장비의 Sector 개념에 대응합니다.
/// </summary>
public record ScanSector {
  /// <summary>Sector 순서 인덱스 (0-based). 웨이퍼 하단(−Y)부터 시작합니다.</summary>
  public int Index { get; }

  /// <summary>
  /// 이 Sector의 스캔 방향.
  /// 짝수 Sector는 LeftToRight, 홀수 Sector는 RightToLeft (Boustrophedon).
  /// </summary>
  public ScanDirection Direction { get; }

  /// <summary>Sector Y축 중심 위치 (웨이퍼 좌표계, µm)</summary>
  public double CenterYum { get; }

  /// <summary>
  /// 이 Sector에 포함된 Shot 목록.
  /// Direction에 따라 X 오름차순(LeftToRight) 또는 내림차순(RightToLeft)으로 정렬됩니다.
  /// </summary>
  public IReadOnlyList<ScanShot> Shots { get; }

  /// <summary>이 Sector의 Shot 총 개수</summary>
  public int ShotCount => Shots.Count;

  public ScanSector(int index, ScanDirection direction, double centerYum, IReadOnlyList<ScanShot> shots) {
    Index      = index;
    Direction  = direction;
    CenterYum  = centerYum;
    Shots      = shots;
  }

  public override string ToString() =>
    $"Sector[{Index}] {Direction} Y={CenterYum}µm Shots={ShotCount}";
}
