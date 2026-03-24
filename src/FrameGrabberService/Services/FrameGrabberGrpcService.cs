using System.Threading.Channels;
using Grpc.Core;
using FrameGrabberService.Grabbers;
using FrameGrabberService.SharedMemory;

using DomainAcquisitionMode = FrameGrabberService.Grabbers.AcquisitionMode;
using DomainPixelFormat      = FrameGrabberService.Grabbers.PixelFormat;
using DomainGrabberState     = FrameGrabberService.Grabbers.GrabberState;

namespace FrameGrabberService.Services;

public sealed class FrameGrabberGrpcService : FrameGrabber.FrameGrabberBase
{
    private readonly IFrameGrabber          _grabber;
    private readonly SharedMemoryRingBuffer _ringBuffer;
    private readonly ILogger<FrameGrabberGrpcService> _logger;

    public FrameGrabberGrpcService(
        IFrameGrabber                    grabber,
        SharedMemoryRingBuffer           ringBuffer,
        ILogger<FrameGrabberGrpcService> logger)
    {
        _grabber    = grabber;
        _ringBuffer = ringBuffer;
        _logger     = logger;
    }

    // ── RPCs ─────────────────────────────────────────────────────────────────

    public override async Task<ConfigureResponse> Configure(
        ConfigureRequest request, ServerCallContext context)
    {
        try
        {
            var config = new GrabberConfig(
                Mode:        ToMode(request.Mode),
                PixelFormat: ToPixelFormat(request.PixelFormat),
                Width:       request.Width       > 0 ? request.Width       : GrabberConfig.Default.Width,
                Height:      request.Height      > 0 ? request.Height      : GrabberConfig.Default.Height,
                FrameRateHz: request.FrameRateHz > 0 ? request.FrameRateHz : GrabberConfig.Default.FrameRateHz);

            await _grabber.ConfigureAsync(config, context.CancellationToken);
            _logger.LogInformation("Grabber configured: {Config}", config);
            return new ConfigureResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configure failed");
            return new ConfigureResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<StartAcquisitionResponse> StartAcquisition(
        StartAcquisitionRequest request, ServerCallContext context)
    {
        try
        {
            await _grabber.StartAsync(context.CancellationToken);
            _logger.LogInformation("Acquisition started");
            return new StartAcquisitionResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartAcquisition failed");
            return new StartAcquisitionResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<StopAcquisitionResponse> StopAcquisition(
        StopAcquisitionRequest request, ServerCallContext context)
    {
        try
        {
            await _grabber.StopAsync(context.CancellationToken);
            _logger.LogInformation("Acquisition stopped");
            return new StopAcquisitionResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StopAcquisition failed");
            return new StopAcquisitionResponse { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// 즉시 프레임 1개를 캡처한다.
    /// TriggerAsync()로 Channel에 프레임을 밀어 넣고,
    /// FramePumpService가 링버퍼에 기록 후 발행하는 FrameGrabbed 이벤트를 1회 수신한다.
    /// </summary>
    public override async Task<FrameHandle> TriggerFrame(
        TriggerFrameRequest request, ServerCallContext context)
    {
        var tcs = new TaskCompletionSource<FrameInfo>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(FrameInfo info)
        {
            _ringBuffer.FrameGrabbed -= Handler;
            tcs.TrySetResult(info);
        }

        _ringBuffer.FrameGrabbed += Handler;

        try
        {
            await _grabber.TriggerAsync(context.CancellationToken);
            var info = await tcs.Task.WaitAsync(context.CancellationToken);
            _logger.LogDebug("TriggerFrame → {FrameId} slot={Slot} seq={Seq}",
                info.FrameId, info.SlotIndex, info.Sequence);
            return ToProtoHandle(info);
        }
        catch
        {
            _ringBuffer.FrameGrabbed -= Handler;
            throw;
        }
    }

    /// <summary>
    /// 링버퍼에 프레임이 기록될 때마다 FrameHandle을 스트리밍한다.
    /// 클라이언트 연결이 끊기면 이벤트 구독을 해제한다.
    /// </summary>
    public override async Task SubscribeFrames(
        SubscribeFramesRequest           request,
        IServerStreamWriter<FrameHandle> responseStream,
        ServerCallContext                context)
    {
        _logger.LogInformation("SubscribeFrames started");

        var channel = Channel.CreateUnbounded<FrameInfo>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        void Handler(FrameInfo info) => channel.Writer.TryWrite(info);
        _ringBuffer.FrameGrabbed += Handler;

        try
        {
            await foreach (var info in channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(ToProtoHandle(info), context.CancellationToken);
                _logger.LogDebug("SubscribeFrames → {FrameId} slot={Slot} seq={Seq}",
                    info.FrameId, info.SlotIndex, info.Sequence);
            }
        }
        finally
        {
            _ringBuffer.FrameGrabbed -= Handler;
            channel.Writer.TryComplete();
            _logger.LogInformation("SubscribeFrames ended");
        }
    }

    public override Task<StatusResponse> GetStatus(
        GetStatusRequest request, ServerCallContext context)
    {
        var s = _grabber.GetStatus();
        return Task.FromResult(new StatusResponse
        {
            State         = ToProtoState(s.State),
            Mode          = ToProtoMode(s.Mode),
            FramesGrabbed = s.FramesGrabbed,
            Message       = s.Message ?? string.Empty
        });
    }

    // ── 매핑 ─────────────────────────────────────────────────────────────────

    private static FrameHandle ToProtoHandle(FrameInfo info) => new()
    {
        FrameId         = info.FrameId,
        SharedMemoryKey = info.SharedMemoryKey,
        TimestampUs     = info.TimestampUs,
        Width           = info.Width,
        Height          = info.Height,
        PixelFormat     = ToProtoPixelFormat(info.PixelFormat),
        Stride          = info.Stride,
        SizeBytes       = info.SizeBytes,
        SlotIndex       = info.SlotIndex,
        Sequence        = info.Sequence
    };

    private static DomainAcquisitionMode ToMode(AcquisitionMode proto) => proto switch
    {
        AcquisitionMode.Triggered => DomainAcquisitionMode.Triggered,
        _                         => DomainAcquisitionMode.Continuous
    };

    private static DomainPixelFormat ToPixelFormat(PixelFormat proto) => proto switch
    {
        PixelFormat.Rgb8 => DomainPixelFormat.Rgb8,
        PixelFormat.Bgr8 => DomainPixelFormat.Bgr8,
        _                => DomainPixelFormat.Mono8
    };

    private static AcquisitionMode ToProtoMode(DomainAcquisitionMode mode) => mode switch
    {
        DomainAcquisitionMode.Triggered => AcquisitionMode.Triggered,
        _                               => AcquisitionMode.Continuous
    };

    private static PixelFormat ToProtoPixelFormat(DomainPixelFormat fmt) => fmt switch
    {
        DomainPixelFormat.Rgb8 => PixelFormat.Rgb8,
        DomainPixelFormat.Bgr8 => PixelFormat.Bgr8,
        _                      => PixelFormat.Mono8
    };

    private static GrabberState ToProtoState(DomainGrabberState state) => state switch
    {
        DomainGrabberState.Acquiring => GrabberState.Acquiring,
        DomainGrabberState.Error     => GrabberState.Error,
        _                            => GrabberState.Idle
    };
}
