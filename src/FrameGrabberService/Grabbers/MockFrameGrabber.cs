using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace FrameGrabberService.Grabbers;

public sealed class MockFrameGrabber : IFrameGrabber
{
    private GrabberConfig _config = GrabberConfig.Default;
    private GrabberState  _state  = GrabberState.Idle;
    private long          _frameCount;

    private Channel<GrabbedFrame>    _channel = CreateChannel();
    private CancellationTokenSource? _continuousCts;

    // ── IFrameGrabber ────────────────────────────────────────────────────────

    public GrabberStatus GetStatus() =>
        new(_state, _config.Mode, Interlocked.Read(ref _frameCount));

    public Task ConfigureAsync(GrabberConfig config, CancellationToken ct = default)
    {
        if (_state == GrabberState.Acquiring)
            throw new InvalidOperationException("Cannot configure while acquiring.");

        _config  = config;
        _channel = CreateChannel();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_state == GrabberState.Acquiring) return Task.CompletedTask;

        _state = GrabberState.Acquiring;

        if (_config.Mode == AcquisitionMode.Continuous)
        {
            _continuousCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = RunContinuousLoopAsync(_continuousCts.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_continuousCts is not null)
        {
            await _continuousCts.CancelAsync();
            _continuousCts.Dispose();
            _continuousCts = null;
        }

        _state = GrabberState.Idle;
    }

    public async Task<GrabbedFrame> TriggerAsync(CancellationToken ct = default)
    {
        var frame = BuildFrame();
        await _channel.Writer.WriteAsync(frame, ct);
        return frame;
    }

    public async IAsyncEnumerable<GrabbedFrame> GetFramesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var frame in _channel.Reader.ReadAllAsync(ct))
            yield return frame;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _channel.Writer.TryComplete();
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private async Task RunContinuousLoopAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(1.0 / _config.FrameRateHz);

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var frame = BuildFrame();
                await _channel.Writer.WriteAsync(frame, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _state = GrabberState.Idle;
        }
    }

    private GrabbedFrame BuildFrame()
    {
        long   index  = Interlocked.Increment(ref _frameCount);
        int    bpp    = BytesPerPixel(_config.PixelFormat);
        int    stride = _config.Width * bpp;
        byte[] data   = GenerateGradient(index, stride);

        return new GrabbedFrame(
            FrameId:     $"frame_{index:D8}",
            PixelData:   data,
            Width:       _config.Width,
            Height:      _config.Height,
            PixelFormat: _config.PixelFormat,
            Stride:      stride,
            Timestamp:   DateTimeOffset.UtcNow);
    }

    private byte[] GenerateGradient(long frameIndex, int stride)
    {
        int    bpp    = BytesPerPixel(_config.PixelFormat);
        byte[] data   = new byte[stride * _config.Height];
        int    offset = (int)(frameIndex * 2 % 256);

        for (int y = 0; y < _config.Height; y++)
        for (int x = 0; x < _config.Width;  x++)
        {
            byte v   = (byte)((x + y + offset) % 256);
            int  idx = y * stride + x * bpp;

            if (_config.PixelFormat == PixelFormat.Mono8)
            {
                data[idx] = v;
            }
            else
            {
                data[idx]     = v;
                data[idx + 1] = (byte)((v + 85)  % 256);
                data[idx + 2] = (byte)((v + 170) % 256);
            }
        }

        return data;
    }

    private static int BytesPerPixel(PixelFormat fmt) => fmt switch
    {
        PixelFormat.Mono8              => 1,
        PixelFormat.Rgb8 or PixelFormat.Bgr8 => 3,
        _                              => 1
    };

    private static Channel<GrabbedFrame> CreateChannel() =>
        Channel.CreateBounded<GrabbedFrame>(new BoundedChannelOptions(32)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = false
        });
}
