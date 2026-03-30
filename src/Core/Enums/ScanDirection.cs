namespace Core.Enums;

/// <summary>
/// Sector 내 Shot 촬영 방향을 나타냅니다.
/// Boustrophedon 경로에서 홀수/짝수 Sector의 스캔 방향을 구분합니다.
/// </summary>
public enum ScanDirection {
  /// <summary>좌 → 우 (+X 방향)</summary>
  LeftToRight = 0,

  /// <summary>우 → 좌 (−X 방향)</summary>
  RightToLeft = 1,
}
