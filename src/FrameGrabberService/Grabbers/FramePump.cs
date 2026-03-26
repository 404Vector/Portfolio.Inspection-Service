using Core.FrameGrabber.Interfaces;
using Core.SharedMemory;

namespace FrameGrabberService.Grabbers;

/// <summary>
/// IFrameGrabber의 프레임 스트림을 SharedMemoryRingBuffer에 기록하는 단일 프로듀서.
/// BackgroundService에 의존하지 않는 순수 클래스. 외부에서 lifecycle을 제어한다.
/// </summary>
public sealed class FramePump : IAsyncDisposable
{
    private readonly IFrameGrabber          _grabber;
    private readonly SharedMemoryRingBuffer _ringBuffer;
    private readonly ILogger<FramePump>     _logger;

    private CancellationTokenSource? _cts;
    private Task?                    _pumpTask;

    public FramePump(
        IFrameGrabber          grabber,
        SharedMemoryRingBuffer ringBuffer,
        ILogger<FramePump>     logger)
    {
        _grabber    = grabber;
        _ringBuffer = ringBuffer;
        _logger     = logger;
    }

    public bool IsRunning => _pumpTask is { IsCompleted: false };

    /// <summary>
    /// 프레임이 링버퍼에 기록된 직후 발행된다.
    /// TriggerFrame RPC 등 단일 프레임 완료를 기다리는 구독자가 사용한다.
    /// </summary>
    public event Action<FrameInfo>? FrameWritten;

    /// <summary>
    /// 프레임 펌프를 시작한다. 이미 실행 중이면 무시한다.
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        _cts      = new CancellationTokenSource();
        _pumpTask = RunAsync(_cts.Token);
        _logger.LogInformation("FramePump started");
    }

    /// <summary>
    /// 프레임 펌프를 중단하고 완료를 기다린다.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts is null) return;

        await _cts.CancelAsync();

        if (_pumpTask is not null)
            await _pumpTask.ConfigureAwait(false);

        _cts.Dispose();
        _cts      = null;
        _pumpTask = null;
        _logger.LogInformation("FramePump stopped");
    }

    public async ValueTask DisposeAsync() => await StopAsync();

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
