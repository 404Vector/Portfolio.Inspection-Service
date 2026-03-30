namespace Core.Enums;

/// <summary>
/// 웨이퍼 직경 표준 타입을 나타냅니다.
/// SEMI 표준 M1에서 정의하는 직경 규격을 열거형으로 정의합니다.
/// </summary>
public enum WaferType {
  Unknown = 0,

  /// <summary>200 mm (8인치) 웨이퍼</summary>
  Wafer200mm = 1,

  /// <summary>300 mm (12인치) 웨이퍼</summary>
  Wafer300mm = 2,
}
