using Core.Enums;

namespace Core.Models;

/// <summary>
/// 웨이퍼의 물리적·식별 정보를 담는 불변 데이터 모델입니다.
/// </summary>
/// <param name="WaferId">웨이퍼 고유 식별자 (예: 슬롯 번호 기반 ID 또는 바코드)</param>
/// <param name="LotId">웨이퍼가 속한 Lot 식별자</param>
/// <param name="SlotIndex">Lot 내 슬롯 번호 (1-based)</param>
/// <param name="WaferType">웨이퍼 직경 표준 타입 (200 mm / 300 mm)</param>
/// <param name="ThicknessUm">웨이퍼 두께 (µm)</param>
/// <param name="Grade">웨이퍼 품질 등급</param>
/// <param name="NotchOrientation">노치 방향. 플랫존 웨이퍼의 경우 플랫이 향하는 방향을 기록합니다.</param>
/// <param name="CoordinateOrigin">웨이퍼 좌표계 원점의 물리적 위치 (µm). 통상 웨이퍼 중심.</param>
/// <param name="DieSize">Die 한 개의 크기 (µm × µm)</param>
/// <param name="DieOffset">
/// Die 격자의 원점 오프셋 (µm).
/// 웨이퍼 중심 대비 첫 번째 Die 기준점의 상대 위치입니다.
/// </param>
/// <param name="WaferOffset">
/// 웨이퍼 기계 원점 대비 실제 웨이퍼 중심의 오프셋 (µm).
/// 척(Chuck) 또는 스테이지 로딩 오차를 보정할 때 사용합니다.
/// </param>
/// <param name="ProcessStep">현재 공정 단계 (예: "Litho", "Etch", "CMP")</param>
/// <param name="CreatedAt">웨이퍼 정보 생성 시각</param>
public record WaferInfo(
  string           WaferId,
  string           LotId,
  int              SlotIndex,
  WaferType        WaferType,
  double           ThicknessUm,
  WaferGrade       Grade,
  NotchOrientation NotchOrientation,
  WaferCoordinate  CoordinateOrigin,
  DieSize          DieSize,
  WaferCoordinate  DieOffset,
  WaferCoordinate  WaferOffset,
  string           ProcessStep,
  DateTimeOffset   CreatedAt
) {
  /// <summary>웨이퍼 직경 (mm). WaferType으로부터 파생됩니다.</summary>
  public double DiameterMm => WaferType switch {
    WaferType.Wafer200mm => 200.0,
    WaferType.Wafer300mm => 300.0,
    _ => throw new InvalidOperationException($"DiameterMm을 결정할 수 없는 WaferType입니다: {WaferType}"),
  };

  /// <summary>웨이퍼 반지름 (mm)</summary>
  public double RadiusMm => DiameterMm / 2.0;

  /// <summary>웨이퍼 반지름 (µm)</summary>
  public double RadiusUm => RadiusMm * 1_000.0;

  /// <summary>웨이퍼 면적 (mm²)</summary>
  public double AreaMm2 => Math.PI * RadiusMm * RadiusMm;

  /// <summary>
  /// 기본값으로 WaferInfo를 생성합니다. 테스트 또는 더미 웨이퍼에 사용됩니다.
  /// </summary>
  public static WaferInfo CreateDummy(string waferId = "WAFER-DUMMY", string lotId = "LOT-DUMMY") =>
    new(
      WaferId:          waferId,
      LotId:            lotId,
      SlotIndex:        1,
      WaferType:        WaferType.Wafer300mm,
      ThicknessUm:      775.0,
      Grade:            WaferGrade.Dummy,
      NotchOrientation: NotchOrientation.Down,
      CoordinateOrigin: WaferCoordinate.Origin,
      DieSize:          new DieSize(26_000.0, 33_000.0),
      DieOffset:        WaferCoordinate.Origin,
      WaferOffset:      WaferCoordinate.Origin,
      ProcessStep:      "Unknown",
      CreatedAt:        DateTimeOffset.UtcNow
    );
}
