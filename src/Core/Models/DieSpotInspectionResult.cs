using Core.Enums;
using Core.Interfaces;

namespace Core.Models;

/// <summary>
/// DieSpotInspectionRecipe 실행 결과입니다.
/// 단일 Die를 지목한 단발 검사 한 잡에 대응합니다.
/// </summary>
/// <param name="RecipeName">실행한 레시피 이름</param>
/// <param name="Status">검사 판정 결과</param>
/// <param name="StartedAt">검사 시작 시각</param>
/// <param name="CompletedAt">검사 완료 시각</param>
/// <param name="ShotCenter">촬영 중심 좌표 (웨이퍼 좌표계, µm)</param>
/// <param name="Score">알고리즘 출력 점수 (0.0 ~ 1.0). 임계값 재조정 시 재계산 가능.</param>
/// <param name="SavedImagePath">SaveOnFail=true 시 저장된 이미지 경로. 저장하지 않은 경우 null.</param>
public record DieSpotInspectionResult(
  string           RecipeName,
  InspectionStatus Status,
  DateTimeOffset   StartedAt,
  DateTimeOffset   CompletedAt,
  WaferCoordinate  ShotCenter,
  double           Score,
  string?          SavedImagePath
) : IInspectionJobResult;
