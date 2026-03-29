using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using Microsoft.Extensions.Hosting;

namespace InspectionClient.Services;

/// <summary>
/// 주입된 IConnectionProbe 목록을 주기적으로 실행하여 연결 상태를 관리한다.
/// probe 구현(gRPC, HTTP 등)에 무관하게 동작한다.
/// IHostedService로 등록되어 Host 수명 주기에 따라 시작/중단된다.
/// </summary>
public sealed class ConnectionMonitor : IServiceConnectionMonitor, IHostedService
{
  public event EventHandler<ServiceConnectionChangedEventArgs>? StateChanged;

  private readonly Dictionary<string, bool> _states;
  private readonly IReadOnlyList<IConnectionProbe> _probes;
  private readonly ILogService _log;

  private CancellationTokenSource? _cts;

  private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(3);

  public IReadOnlyDictionary<string, bool> States => _states;

  public ConnectionMonitor(
      IEnumerable<IConnectionProbe> probes,
      ILogService logService)
  {
    _probes = probes.ToList();
    _log    = logService;
    _states = _probes.ToDictionary(p => p.ServiceKey, _ => false);
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    _ = Task.Run(() => ProbeLoopAsync(_cts.Token), _cts.Token);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _cts?.Cancel();
    return Task.CompletedTask;
  }

  private async Task ProbeLoopAsync(CancellationToken ct)
  {
    while (!ct.IsCancellationRequested)
    {
      foreach (var probe in _probes)
      {
        if (ct.IsCancellationRequested) break;
        var connected = await probe.CheckAsync(ct);
        SetState(probe.ServiceKey, connected);
      }

      try { await Task.Delay(ProbeInterval, ct); }
      catch (OperationCanceledException) { break; }
    }

    // 종료 시 모든 서비스를 끊김으로 표시
    foreach (var probe in _probes)
      SetState(probe.ServiceKey, false);
  }

  private void SetState(string key, bool connected)
  {
    if (_states.TryGetValue(key, out var current) && current == connected) return;

    _states[key] = connected;

    if (connected)
      _log.Info(this, $"[{key}] connected");
    else
      _log.Warning(this, $"[{key}] disconnected");

    Dispatcher.UIThread.Post(
        () => StateChanged?.Invoke(this, new ServiceConnectionChangedEventArgs(key, connected)),
        DispatcherPriority.Normal);
  }
}
