using Core.Interfaces;

namespace Core.Models;

/// <summary>
/// 단일 프레임에 대한 검사 결과입니다.
/// WaferSurfaceInspectionResult의 구성 요소로 사용됩니다.
/// 프레임의 합격/불합격은 Defects.Count == 0 여부로 판단합니다.
/// </summary>
/// <param name="FrameId">프레임 식별자</param>
/// <param name="FrameIndex">스캔 순서 인덱스 (0-based)</param>
/// <param name="ShotCenter">촬영 FOV 중심 좌표 (웨이퍼 좌표계, µm)</param>
/// <param name="ShotFov">촬영 FOV 크기 (µm). 결함의 웨이퍼 절대 좌표 및 Die 내 상대 좌표 계산에 사용.</param>
/// <param name="CoveredDies">Shot FOV와 겹치는 Die 인덱스 목록. ScanShot.CoveredDies와 대응.</param>
/// <param name="Defects">검출된 결함 목록. 비어 있으면 해당 프레임은 Pass.</param>
/// <param name="InspectedAt">검사 완료 시각</param>
public record FrameInspectionResult(
  string                   FrameId,
  int                      FrameIndex,
  WaferCoordinate          ShotCenter,
  FovSize                  ShotFov,
  IReadOnlyList<DieIndex>  CoveredDies,
  IReadOnlyList<DefectInfo> Defects,
  DateTimeOffset           InspectedAt
) : IInspectionResult {
  /// <summary>결함이 하나라도 검출된 경우 true.</summary>
  public bool HasDefect => Defects.Count > 0;
}
