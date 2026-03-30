using Core.Enums;

namespace Core.Interfaces;

/// <summary>
/// 검사 잡(Job) 전체 결과의 공통 계약입니다.
/// 레시피 한 번 실행에 대응하는 집계 수준의 결과를 나타냅니다.
/// </summary>
public interface IInspectionJobResult {
  string           RecipeName  { get; }
  InspectionStatus Status      { get; }
  DateTimeOffset   StartedAt   { get; }
  DateTimeOffset   CompletedAt { get; }
}
