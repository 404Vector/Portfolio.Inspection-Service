using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Core.FrameGrabber.Interfaces;
using Core.FrameGrabber.Models;
using CE = Core.Enums;

namespace FileFrameGrabberService.Services;

public sealed class FileFrameGrabberService : IFrameGrabber
{
  private readonly GradientFrameSynthesizerService _gradientSynthesizer;
  private readonly FileImageFrameSynthesizerService _imageSynthesizer;
  private readonly GrabberParameterStoreService    _paramStore;

  private readonly Lock _lock = new();

  private IFrameSynthesizerService _activeSynthesizer;
  private GrabberConfig            _config        = GrabberConfig.Default;
  private CE.GrabberState          _state         = CE.GrabberState.Idle;
  private long                     _frameCount;
  private Channel<GrabbedFrame>    _channel       = CreateChannel();
  private CancellationTokenSource? _continuousCts;

  // ── 동적 명령 정의 ────────────────────────────────────────────────────────

  private static readonly IReadOnlyList<CommandDescriptor> SupportedCommands =
  [
    new("reset_counter", "Reset Frame Counter", "프레임 카운터를 0으로 초기화한다."),
    new("snapshot",      "Snapshot",            "현재 모드에 관계없이 프레임을 1장 즉시 캡처한다."),
  ];

  public FileFrameGrabberService(
      GradientFrameSynthesizerService  gradientSynthesizer,
      FileImageFrameSynthesizerService imageSynthesizer,
      GrabberParameterStoreService     paramStore)
  {
    _gradientSynthesizer = gradientSynthesizer;
    _imageSynthesizer    = imageSynthesizer;
    _paramStore          = paramStore;
    _activeSynthesizer   = gradientSynthesizer;
  }

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
    long index = Interlocked.Increment(ref _frameCount);
    GrabberConfig            config;
    IFrameSynthesizerService synthesizer;

    lock (_lock)
    {
      config      = _config;
      synthesizer = _activeSynthesizer;
    }

    var frame = synthesizer.BuildFrame(config, index);
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

  public IReadOnlyList<ParameterDescriptor> GetParameters() => _paramStore.GetParameters();

  public ParameterValue GetParameter(string key)
  {
    GrabberConfig config;
    lock (_lock) config = _config;
    return _paramStore.GetParameter(config, key);
  }

  public async Task SetParameterAsync(string key, ParameterValue value, CancellationToken ct = default)
  {
    // source_mode / image_path는 합성기 교체 로직을 포함하므로 별도 처리한다.
    if (_paramStore.ApplySynthesizerParameter(key, value))
    {
      await ApplySynthesizerChangeAsync(key, ct);
      return;
    }

    lock (_lock)
    {
      if (_state == CE.GrabberState.Acquiring)
        throw new InvalidOperationException("Cannot set parameter while acquiring.");

      _config = _paramStore.ApplyParameter(_config, key, value);
    }
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
  /// source_mode 또는 image_path 변경 후 합성기 상태를 반영한다.
  /// image_path 설정 시 파일을 읽어 FileImageFrameSynthesizerService에 전달한다.
  /// </summary>
  private async Task ApplySynthesizerChangeAsync(string changedKey, CancellationToken ct)
  {
    if (changedKey == "image_path" || _paramStore.SourceMode == "file")
    {
      var path = _paramStore.ImagePath;

      if (!string.IsNullOrEmpty(path))
      {
        var (pixelData, width, height, pixelFormat, stride) =
            await ReadImageFileAsync(path, ct);

        _imageSynthesizer.SetImageSource(pixelData, width, height, pixelFormat, stride);
      }
    }

    lock (_lock)
    {
      _activeSynthesizer = _paramStore.SourceMode switch
      {
        "file"     => _imageSynthesizer,
        "gradient" => _gradientSynthesizer,
        _          => _gradientSynthesizer
      };
    }
  }

  /// <summary>
  /// 이미지 파일을 읽어 원시 픽셀 데이터로 변환한다.
  /// 현재는 단순 바이트 읽기(RAW)만 지원한다.
  /// TODO: 실제 이미지 디코딩(PNG/BMP 등) 라이브러리 연동 시 이 메서드를 교체한다.
  /// </summary>
  private static async Task<(byte[] pixelData, int width, int height, CE.PixelFormat pixelFormat, int stride)>
      ReadImageFileAsync(string path, CancellationToken ct)
  {
    if (!File.Exists(path))
      throw new FileNotFoundException($"Image file not found: '{path}'");

    var pixelData = await File.ReadAllBytesAsync(path, ct);

    // RAW 바이트 파일 가정: 1차원 Mono8 픽셀 데이터
    // 실제 PNG/BMP 디코딩은 SixLabors.ImageSharp 등으로 교체 예정
    int width       = pixelData.Length;
    int height      = 1;
    int stride      = width;
    var pixelFormat = CE.PixelFormat.Mono8;

    return (pixelData, width, height, pixelFormat, stride);
  }

  private async Task RunContinuousLoopAsync(
      GrabberConfig config, Channel<GrabbedFrame> channel, CancellationToken ct)
  {
    var interval = TimeSpan.FromSeconds(1.0 / config.FrameRateHz);

    using var timer = new PeriodicTimer(interval);

    try
    {
      while (await timer.WaitForNextTickAsync(ct))
      {
        IFrameSynthesizerService synthesizer;
        lock (_lock) synthesizer = _activeSynthesizer;

        long index = Interlocked.Increment(ref _frameCount);
        var frame  = synthesizer.BuildFrame(config, index);
        await channel.Writer.WriteAsync(frame, ct);
      }
    }
    catch (OperationCanceledException) { }
  }

  private static Channel<GrabbedFrame> CreateChannel() =>
      Channel.CreateBounded<GrabbedFrame>(new BoundedChannelOptions(32)
      {
        FullMode     = BoundedChannelFullMode.DropOldest,
        SingleWriter = false,
        SingleReader = false
      });
}
