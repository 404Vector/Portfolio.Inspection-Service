namespace Core.Enums;

/// <summary>
/// 웨이퍼 품질 등급을 나타냅니다.
/// SEMI M1 표준 등급 체계를 기반으로 합니다.
/// </summary>
public enum WaferGrade {
  Unknown = 0,

  /// <summary>Prime급 — 최고 품질, 디바이스 제조용</summary>
  Prime = 1,

  /// <summary>Test급 — 공정 테스트·장비 캘리브레이션용</summary>
  Test = 2,

  /// <summary>Dummy급 — 로딩 더미 또는 세정용</summary>
  Dummy = 3,

  /// <summary>Reclaim급 — 재생 웨이퍼</summary>
  Reclaim = 4,
}
