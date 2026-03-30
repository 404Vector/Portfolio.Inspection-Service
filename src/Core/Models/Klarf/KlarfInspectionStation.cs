namespace Core.Models.Klarf;

/// <summary>
/// 검사 장비 식별 정보입니다.
/// KLARF 1.8 InspectionStationID 섹션에 대응합니다.
/// </summary>
/// <param name="ManufacturerName">장비 제조사 이름. KLARF 필드 1.</param>
/// <param name="ModelNumber">장비 모델 번호. KLARF 필드 2.</param>
/// <param name="StationId">장비 고유 식별자 (시리얼 번호 또는 호스트명). KLARF 필드 3.</param>
public record KlarfInspectionStation(
  string ManufacturerName,
  string ModelNumber,
  string StationId
);
