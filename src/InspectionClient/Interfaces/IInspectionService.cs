using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using Core.Recipe.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// InspectionService와 통신하는 클라이언트 계약.
/// 실제 구현은 gRPC 클라이언트, Mock 구현은 로컬 시뮬레이터를 사용한다.
/// </summary>
public interface IInspectionService
{
  /// <summary>
  /// 웨이퍼 검사를 시작한다.
  /// 진행 상황은 <see cref="ProgressChanged"/> 이벤트로 발행된다.
  /// </summary>
  /// <param name="recipe">실행할 검사 레시피</param>
  /// <param name="wafer">검사 대상 웨이퍼 정보. DieMap 계산 및 결과 기록에 사용된다.</param>
  /// <param name="ct">취소 토큰</param>
  Task StartAsync(WaferSurfaceInspectionRecipe recipe, WaferInfo wafer, CancellationToken ct = default);

  /// <summary>
  /// 진행 중인 검사를 중단한다.
  /// </summary>
  Task StopAsync();

  /// <summary>각 ScanShot 완료 시 발행된다.</summary>
  event EventHandler<InspectionProgressEventArgs>? ProgressChanged;

  /// <summary>검사 완료(정상 또는 중단) 시 발행된다.</summary>
  event EventHandler<InspectionCompletedEventArgs>? Completed;
}

/// <summary>
/// 개별 ScanShot 결과 이벤트 인수.
/// </summary>
public sealed class InspectionProgressEventArgs(
    int completedShots,
    int totalShots,
    DieIndex dieIndex,
    bool passed) : EventArgs
{
  /// <summary>완료된 Shot 수.</summary>
  public int CompletedShots { get; } = completedShots;

  /// <summary>전체 Shot 수.</summary>
  public int TotalShots { get; } = totalShots;

  /// <summary>현재 Shot이 속한 Die 인덱스.</summary>
  public DieIndex DieIndex { get; } = dieIndex;

  /// <summary>해당 Shot의 Pass/Fail 결과.</summary>
  public bool Passed { get; } = passed;

  public double ProgressRatio => TotalShots == 0 ? 0.0 : (double)CompletedShots / TotalShots;
}

/// <summary>
/// 검사 완료 이벤트 인수.
/// </summary>
public sealed class InspectionCompletedEventArgs(bool cancelled) : EventArgs
{
  /// <summary>사용자 중단으로 완료된 경우 true.</summary>
  public bool Cancelled { get; } = cancelled;
}
