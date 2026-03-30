namespace Core.Enums;

/// <summary>
/// 웨이퍼 노치(또는 플랫존)의 방향을 나타냅니다.
/// SEMI 표준에서 사용하는 각도 기반 방향 표기를 열거형으로 정의합니다.
/// </summary>
public enum NotchOrientation {
  Unknown = 0,

  /// <summary>노치가 아래쪽 (270°, 표준 SEMI 기준)</summary>
  Down = 1,

  /// <summary>노치가 위쪽 (90°)</summary>
  Up = 2,

  /// <summary>노치가 왼쪽 (180°)</summary>
  Left = 3,

  /// <summary>노치가 오른쪽 (0°)</summary>
  Right = 4,
}
