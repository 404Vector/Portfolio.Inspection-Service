namespace Core.Models.Klarf;

/// <summary>
/// 검사 설정 및 레시피 메타 정보입니다.
/// KLARF 1.8 SetupID, InspectionTest, ResultTimestamp 섹션에 대응합니다.
/// </summary>
/// <param name="SetupId">검사 설정(레시피) 식별자. KLARF SetupID.</param>
/// <param name="StepId">공정 단계 식별자 (예: "Litho", "Etch"). KLARF StepID.</param>
/// <param name="InspectionTest">검사 테스트 번호. KLARF InspectionTest (1-based).</param>
/// <param name="ResultTimestamp">검사 결과 생성 시각. KLARF ResultTimestamp.</param>
public record KlarfInspectionSetup(
  string         SetupId,
  string         StepId,
  int            InspectionTest,
  DateTimeOffset ResultTimestamp
);
