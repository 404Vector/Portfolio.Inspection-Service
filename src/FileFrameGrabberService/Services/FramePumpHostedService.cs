using Core.FrameGrabber.Interfaces;
using Core.SharedMemory.Models;
using Core.SharedMemory.Writer;

namespace FileFrameGrabberService.Services;

/// <summary>
/// IFrameGrabber의 프레임 스트림을 SharedMemoryRingBuffer에 기록하는 단일 프로듀서.
/// IHostedService로 앱 종료 시 Hosting이 StopAsync를 호출하여 정상 종료를 보장한다.
/// Start/StopAsync는 lock으로 보호하여 SPSC 전제를 보장한다.
/// </summary>
public sealed class FramePumpHostedService : IHostedService, IAsyncDisposable
{
  private readonly IFrameGrabber          _grabber;
  private readonly SharedMemoryRingBuffer _ringBuffer;
  private readonly ILogger<FramePumpHostedService> _logger;

  private readonly object              _lock = new();
  private CancellationTokenSource?     _cts;
  private Task?                        _pumpTask;

  public FramePumpHostedService(
      IFrameGrabber                    grabber,
      SharedMemoryRingBuffer           ringBuffer,
      ILogger<FramePumpHostedService>  logger)
  {
    _grabber    = grabber;
    _ringBuffer = ringBuffer;
    _logger     = logger;
  }

  public bool IsRunning
  {
    get { lock (_lock) return _pumpTask is { IsCompleted: false }; }
  }

  /// <summary>
  /// 프레임이 링버퍼에 기록된 직후 발행된다.
  /// TriggerFrame RPC 등 단일 프레임 완료를 기다리는 구독자가 사용한다.
  /// </summary>
  public event Action<FrameInfo>? FrameWritten;

  /// <summary>
  /// IHostedService.StartAsync — 앱 시작 시 Hosting이 호출. 획득은 RPC가 제어하므로 no-op.
  /// </summary>
  public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  /// <summary>
  /// IHostedService.StopAsync — 앱 종료 시 Hosting이 호출.
  /// 펌프를 중단한 뒤 Grabber도 함께 중단하여 연속 루프 누수를 방지한다.
  /// </summary>
  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await StopPumpAsync();
    await _grabber.StopAsync(cancellationToken);
  }

  /// <summary>
  /// 프레임 펌프를 시작한다. 이미 실행 중이면 무시한다.
  /// StartAcquisition RPC에서 호출한다.
  /// </summary>
  public void StartPump()
  {
    lock (_lock)
    {
      if (_pumpTask is { IsCompleted: false }) return;

      _cts      = new CancellationTokenSource();
      _pumpTask = RunAsync(_cts.Token);
    }
    _logger.LogInformation("FramePump started");
  }

  /// <summary>
  /// 프레임 펌프를 중단하고 완료를 기다린다.
  /// StopAcquisition RPC 및 앱 종료 시 호출한다.
  /// </summary>
  public async Task StopPumpAsync()
  {
    CancellationTokenSource? cts;
    Task?                    pumpTask;

    lock (_lock)
    {
      cts       = _cts;
      pumpTask  = _pumpTask;
      _cts      = null;
      _pumpTask = null;
    }

    if (cts is null) return;

    await cts.CancelAsync();

    if (pumpTask is not null)
      await pumpTask.ConfigureAwait(false);

    cts.Dispose();
    _logger.LogInformation("FramePump stopped");
  }

  public async ValueTask DisposeAsync() => await StopPumpAsync();

  // ── Internals ────────────────────────────────────────────────────────────

  private async Task RunAsync(CancellationToken ct)
  {
    try
    {
      await foreach (var frame in _grabber.GetFramesAsync(ct))
      {
        var info = _ringBuffer.Write(
            frame.FrameId,
            frame.PixelData,
            frame.Width,
            frame.Height,
            frame.PixelFormat,
            frame.Stride,
            frame.Timestamp);
        FrameWritten?.Invoke(info);
      }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
      _logger.LogError(ex, "FramePump faulted");
    }
  }
}
