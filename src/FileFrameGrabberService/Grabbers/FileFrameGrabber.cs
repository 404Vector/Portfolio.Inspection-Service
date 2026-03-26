using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Core.FrameGrabber.Interfaces;
using Core.FrameGrabber.Models;
using CE = Core.Enums;

namespace FileFrameGrabberService.Grabbers;

public sealed class FileFrameGrabber : IFrameGrabber
{
    private readonly object _lock = new();

    private GrabberConfig            _config        = GrabberConfig.Default;
    private CE.GrabberState          _state         = CE.GrabberState.Idle;
    private long                     _frameCount;
    private Channel<GrabbedFrame>    _channel       = CreateChannel();
    private CancellationTokenSource? _continuousCts;

    // ── 동적 파라미터 정의 ────────────────────────────────────────────────────

    private static readonly IReadOnlyList<ParameterDescriptor> SupportedParameters =
    [
        new("frame_rate_hz", "Frame Rate (Hz)", ParameterValueType.Double, MinValue: 1.0,  MaxValue: 1000.0, DefaultValue: 30.0),
        new("width",         "Width (px)",      ParameterValueType.Int64,  MinValue: 1L,   MaxValue: 16384L, DefaultValue: 1280L),
        new("height",        "Height (px)",     ParameterValueType.Int64,  MinValue: 1L,   MaxValue: 16384L, DefaultValue: 1024L),
        new("pixel_format",  "Pixel Format",    ParameterValueType.String, MinValue: null, MaxValue: null,   DefaultValue: "Mono8"),
    ];

    // ── 동적 명령 정의 ────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<CommandDescriptor> SupportedCommands =
    [
        new("reset_counter", "Reset Frame Counter", "프레임 카운터를 0으로 초기화한다."),
        new("snapshot",      "Snapshot",            "현재 모드에 관계없이 프레임을 1장 즉시 캡처한다."),
    ];

    // ── IFrameGrabber — 상태 ─────────────────────────────────────────────────

    public GrabberStatus GetStatus()
    {
        lock (_lock) return new(_state, _config.Mode, Interlocked.Read(ref _frameCount));
    }

    // ── IFrameGrabber — 획득 제어 ─────────────────────────────────────────────

    public Task ConfigureAsync(GrabberConfig config, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_state == CE.GrabberState.Acquiring)
                throw new InvalidOperationException("Cannot configure while acquiring.");

            _config  = config;
            _channel = CreateChannel();
        }
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_state == CE.GrabberState.Acquiring) return Task.CompletedTask;

            _state = CE.GrabberState.Acquiring;

            if (_config.Mode == CE.AcquisitionMode.Continuous)
            {
                _continuousCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _ = RunContinuousLoopAsync(_config, _channel, _continuousCts.Token);
            }
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        CancellationTokenSource? cts;

        lock (_lock)
        {
            cts            = _continuousCts;
            _continuousCts = null;
            _state         = CE.GrabberState.Idle;
        }

        if (cts is not null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
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

    // ── IFrameGrabber — 동적 파라미터 ─────────────────────────────────────────

    public IReadOnlyList<ParameterDescriptor> GetParameters() => SupportedParameters;

    public ParameterValue GetParameter(string key)
    {
        GrabberConfig config;
        lock (_lock) config = _config;

        return key switch
        {
            "frame_rate_hz" => new ParameterValue.DoubleValue(config.FrameRateHz),
            "width"         => new ParameterValue.Int64Value(config.Width),
            "height"        => new ParameterValue.Int64Value(config.Height),
            "pixel_format"  => new ParameterValue.StringValue(config.PixelFormat.ToString()),
            _               => throw new KeyNotFoundException($"Unknown parameter: '{key}'")
        };
    }

    public Task SetParameterAsync(string key, ParameterValue value, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_state == CE.GrabberState.Acquiring)
                throw new InvalidOperationException("Cannot set parameter while acquiring.");

            _config = key switch
            {
                "frame_rate_hz" => value is ParameterValue.DoubleValue d
                    ? _config with { FrameRateHz = ValidateRange(d.Value, 1.0, 1000.0, key) }
                    : throw new ArgumentException($"Expected Double for '{key}'"),

                "width" => value is ParameterValue.Int64Value w
                    ? _config with { Width = (int)ValidateRange(w.Value, 1, 16384, key) }
                    : throw new ArgumentException($"Expected Int64 for '{key}'"),

                "height" => value is ParameterValue.Int64Value h
                    ? _config with { Height = (int)ValidateRange(h.Value, 1, 16384, key) }
                    : throw new ArgumentException($"Expected Int64 for '{key}'"),

                "pixel_format" => value is ParameterValue.StringValue s
                    ? _config with { PixelFormat = ParsePixelFormat(s.Value) }
                    : throw new ArgumentException($"Expected String for '{key}'"),

                _ => throw new KeyNotFoundException($"Unknown parameter: '{key}'")
            };
        }
        return Task.CompletedTask;
    }

    // ── IFrameGrabber — 동적 명령 ─────────────────────────────────────────────

    public IReadOnlyList<CommandDescriptor> GetCommands() => SupportedCommands;

    public async Task<CommandResult> ExecuteCommandAsync(string command, CancellationToken ct = default)
    {
        switch (command)
        {
            case "reset_counter":
                Interlocked.Exchange(ref _frameCount, 0);
                return new CommandResult(Success: true);

            case "snapshot":
                var frame = await TriggerAsync(ct);
                return new CommandResult(
                    Success:     true,
                    ReturnValue: new ParameterValue.StringValue(frame.FrameId));

            default:
                throw new KeyNotFoundException($"Unknown command: '{command}'");
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>
    /// config와 channel을 시작 시점의 로컬 복사본으로 받아 루프 내 일관성을 보장한다.
    /// </summary>
    private async Task RunContinuousLoopAsync(
        GrabberConfig config, Channel<GrabbedFrame> channel, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(1.0 / config.FrameRateHz);

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var frame = BuildFrame();
                await channel.Writer.WriteAsync(frame, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private GrabbedFrame BuildFrame()
    {
        GrabberConfig config;
        lock (_lock) config = _config;

        long   index  = Interlocked.Increment(ref _frameCount);
        int    bpp    = BytesPerPixel(config.PixelFormat);
        int    stride = config.Width * bpp;
        byte[] data   = GenerateGradient(config, index, stride);

        return new GrabbedFrame(
            FrameId:     $"frame_{index:D8}",
            PixelData:   data,
            Width:       config.Width,
            Height:      config.Height,
            PixelFormat: config.PixelFormat,
            Stride:      stride,
            Timestamp:   DateTimeOffset.UtcNow);
    }

    private static byte[] GenerateGradient(GrabberConfig config, long frameIndex, int stride)
    {
        int    bpp    = BytesPerPixel(config.PixelFormat);
        byte[] data   = new byte[stride * config.Height];
        int    offset = (int)(frameIndex * 2 % 256);

        for (int y = 0; y < config.Height; y++)
        for (int x = 0; x < config.Width;  x++)
        {
            byte v   = (byte)((x + y + offset) % 256);
            int  idx = y * stride + x * bpp;

            if (config.PixelFormat == CE.PixelFormat.Mono8)
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

    private static int BytesPerPixel(CE.PixelFormat fmt) => fmt switch
    {
        CE.PixelFormat.Mono8                        => 1,
        CE.PixelFormat.Rgb8 or CE.PixelFormat.Bgr8 => 3,
        _                                           => 1
    };

    private static Channel<GrabbedFrame> CreateChannel() =>
        Channel.CreateBounded<GrabbedFrame>(new BoundedChannelOptions(32)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = false
        });

    private static T ValidateRange<T>(T value, T min, T max, string key)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentException($"Parameter '{key}' value {value} is out of range [{min}, {max}]");
        return value;
    }

    private static CE.PixelFormat ParsePixelFormat(string value) =>
        value switch
        {
            "Mono8" => CE.PixelFormat.Mono8,
            "Rgb8"  => CE.PixelFormat.Rgb8,
            "Bgr8"  => CE.PixelFormat.Bgr8,
            _       => throw new ArgumentException($"Unknown pixel format: '{value}'. Valid values: Mono8, Rgb8, Bgr8")
        };
}
