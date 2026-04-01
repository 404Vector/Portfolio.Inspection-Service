using Core.Enums;
using Core.Interfaces;

namespace Core.Models;

/// <summary>
/// WaferSurfaceInspectionRecipe 실행 결과입니다.
/// 웨이퍼 전체 Surface 스캔 한 잡에 대응하며, 프레임별 결과를 집계합니다.
/// </summary>
/// <param name="RecipeName">실행한 레시피 이름</param>
/// <param name="WaferId">검사 대상 웨이퍼 식별자. KLARF 헤더(LotInformation, WaferInformation) 생성에 사용.</param>
/// <param name="Status">잡 전체 판정 결과. Aborted/Error는 결함 집계로 판단할 수 없으므로 별도 기록.</param>
/// <param name="StartedAt">검사 시작 시각</param>
/// <param name="CompletedAt">검사 완료 시각</param>
/// <param name="FrameResults">프레임별 검사 결과 목록 (스캔 순서 정렬)</param>
public record WaferSurfaceInspectionResult(
  string                               RecipeName,
  string                               WaferId,
  InspectionStatus                     Status,
  DateTimeOffset                       StartedAt,
  DateTimeOffset                       CompletedAt,
  IReadOnlyList<FrameInspectionResult> FrameResults
) : IInspectionJobResult {
  /// <summary>전체 촬영 프레임 수</summary>
  public int TotalFrames => FrameResults.Count;

  /// <summary>결함이 하나 이상 검출된 프레임 수</summary>
  public int DefectFrameCount => FrameResults.Count(f => f.HasDefect);

  /// <summary>전체 결함 수 (모든 프레임의 Defects 합산)</summary>
  public int TotalDefectCount => FrameResults.Sum(f => f.Defects.Count);
}
