using FrameGrabberService.Grabbers;
using FrameGrabberService.SharedMemory;

namespace FrameGrabberService.Services;

/// <summary>
/// IFrameGrabber의 프레임을 SharedMemoryRingBuffer에 단일 스트림으로 기록하는 백그라운드 서비스.
/// 링버퍼의 단일 프로듀서(SPSC) 전제를 보장한다.
/// </summary>
public sealed class FramePumpService : BackgroundService
{
    private readonly IFrameGrabber          _grabber;
    private readonly SharedMemoryRingBuffer _ringBuffer;
    private readonly ILogger<FramePumpService> _logger;

    public FramePumpService(
        IFrameGrabber              grabber,
        SharedMemoryRingBuffer     ringBuffer,
        ILogger<FramePumpService>  logger)
    {
        _grabber    = grabber;
        _ringBuffer = ringBuffer;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FramePumpService started");

        try
        {
            await foreach (var frame in _grabber.GetFramesAsync(stoppingToken))
            {
                _ringBuffer.Write(frame);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FramePumpService faulted");
        }

        _logger.LogInformation("FramePumpService stopped");
    }
}
