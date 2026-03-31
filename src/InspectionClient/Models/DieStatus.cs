using Core.Models;

namespace InspectionClient.Models;

/// <summary>
/// WaferMap 렌더링에 사용되는 Die의 검사 상태.
/// </summary>
public enum DieInspectionState
{
  /// <summary>아직 검사되지 않은 Die.</summary>
  Pending,
  /// <summary>현재 검사 중인 Die.</summary>
  Current,
  /// <summary>검사 통과.</summary>
  Pass,
  /// <summary>검사 실패.</summary>
  Fail,
}

/// <summary>
/// Die 인덱스와 검사 상태를 묶은 UI 전용 모델.
/// </summary>
public sealed record DieStatus(DieIndex Index, DieInspectionState State);
