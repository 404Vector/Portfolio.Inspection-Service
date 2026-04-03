using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionRecipe.Models;

namespace InspectionClient.Services;

/// <summary>
/// InspectionService gRPC 클라이언트의 Mock 구현.
///
/// DieMap을 기반으로 Boustrophedon 순서에 따라 Shot을 시뮬레이션한다.
/// 각 Shot은 <see cref="ShotIntervalMs"/> 간격으로 처리되며
/// <see cref="FailProbability"/> 확률로 Fail 판정을 발행한다.
///
/// 이벤트는 Avalonia UI 스레드에서 발행된다.
/// </summary>
public sealed class MockInspectionService : IInspectionService
{
  /// <summary>Shot 간 처리 간격 (ms). 기본 200 ms.</summary>
  public int ShotIntervalMs { get; set; } = 200;

  /// <summary>Shot Fail 확률 (0.0 ~ 1.0). 기본 0.1 (10%).</summary>
  public double FailProbability { get; set; } = 0.1;

  public event EventHandler<InspectionProgressEventArgs>? ProgressChanged;
  public event EventHandler<InspectionCompletedEventArgs>? Completed;

  private CancellationTokenSource? _cts;
  private readonly Random _random = new();

  public Task StartAsync(WaferSurfaceInspectionRecipe recipe, WaferInfo wafer, CancellationToken ct = default)
  {
    if (_cts is not null)
      throw new InvalidOperationException("이미 검사가 진행 중입니다.");

    _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    var token = _cts.Token;

    _ = Task.Run(() => RunAsync(wafer, token), token);
    return Task.CompletedTask;
  }

  public Task StopAsync()
  {
    _cts?.Cancel();
    return Task.CompletedTask;
  }

  private async Task RunAsync(WaferInfo wafer, CancellationToken ct)
  {
    bool cancelled = false;
    try
    {
      var dieMap = DieMap.From(wafer);
      var dies   = dieMap.ActiveDies;
      int total  = dies.Count;

      for (int i = 0; i < total; i++)
      {
        ct.ThrowIfCancellationRequested();

        await Task.Delay(ShotIntervalMs, ct);

        var die    = dies[i];
        bool passed = _random.NextDouble() >= FailProbability;
        int completed = i + 1;

        var args = new InspectionProgressEventArgs(completed, total, die.Index, passed);
        Dispatcher.UIThread.Post(() => ProgressChanged?.Invoke(this, args));
      }
    }
    catch (OperationCanceledException)
    {
      cancelled = true;
    }
    finally
    {
      _cts?.Dispose();
      _cts = null;

      var completedArgs = new InspectionCompletedEventArgs(cancelled);
      Dispatcher.UIThread.Post(() => Completed?.Invoke(this, completedArgs));
    }
  }
}
