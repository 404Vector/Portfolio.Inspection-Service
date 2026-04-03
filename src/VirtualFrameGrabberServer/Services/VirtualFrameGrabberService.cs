using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Core.FrameGrabber.Interfaces;
using Core.FrameGrabber.Models;
using Core.Models;
using VirtualFrameGrabberServer.Interfaces;
using CE = Core.Enums;

namespace VirtualFrameGrabberServer.Services;

/// <summary>
/// source_mode에 따라 프레임 생성 전략이 달라지는 가상 프레임 그래버.
///   - "gradient": GradientFrameSynthesizerService로 합성 패턴 프레임 생성
///   - "scan":     웨이퍼 이미지 + ScanPlan 기반 Shot별 FOV crop
/// </summary>
public sealed class VirtualFrameGrabberService : IFrameGrabber
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  private readonly GradientFrameSynthesizerService _gradientSynthesizer;
  private readonly GrabberParameterStoreService    _paramStore;

  private readonly Lock _lock = new();

  private GrabberConfig       _config     = GrabberConfig.Default;
  private CE.GrabberState     _state      = CE.GrabberState.Idle;
  private long                _frameCount;
  private Channel<GrabbedFrame> _channel  = CreateChannel();
  private CancellationTokenSource? _continuousCts;

  // ── Scan 모드 상태 ───────────────────────────────────────────────────────

  private byte[]?        _waferImageData;
  private int            _waferImageWidth;
  private int            _waferImageHeight;
  private CE.PixelFormat _waferImagePixelFormat;
  private int            _waferImageStride;

  private ScanPlan?      _scanPlan;

  private IReadOnlyList<ScanShot> _flatShots = Array.Empty<ScanShot>();
  private int _currentShotIndex;

  // ── 동적 명령 정의 ────────────────────────────────────────────────────────

  private static readonly IReadOnlyList<CommandDescriptor> SupportedCommands =
  [
    new("reset_counter", "Reset Frame Counter", "프레임 카운터를 0으로 초기화한다."),
    new("snapshot",      "Snapshot",            "현재 모드에 관계없이 프레임을 1장 즉시 캡처한다."),
  ];

  public VirtualFrameGrabberService(
      GradientFrameSynthesizerService gradientSynthesizer,
      GrabberParameterStoreService    paramStore)
  {
    _gradientSynthesizer = gradientSynthesizer;
    _paramStore          = paramStore;
  }

  private bool IsScanMode => _paramStore.SourceMode == "scan";

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

      if (IsScanMode)
      {
        ValidateScanReady();
        _currentShotIndex = 0;
      }

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
    GrabbedFrame frame;
    long index = Interlocked.Increment(ref _frameCount);

    if (IsScanMode)
    {
      ScanShot shot;
      GrabberConfig config;

      lock (_lock)
      {
        if (_scanPlan is null || _waferImageData is null)
          throw new InvalidOperationException(
              "Wafer image and ScanPlan must be set before triggering.");

        if (_currentShotIndex >= _flatShots.Count)
          throw new InvalidOperationException("All shots have been acquired.");

        shot   = _flatShots[_currentShotIndex];
        config = _config;
      }

      frame = CropShotFrame(shot, config.PixelFormat, index);

      lock (_lock)
      {
        _currentShotIndex++;
        if (_currentShotIndex >= _flatShots.Count)
          _state = CE.GrabberState.Idle;
      }
    }
    else
    {
      GrabberConfig config;
      lock (_lock) config = _config;
      frame = _gradientSynthesizer.BuildFrame(config, index);
    }

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

  public IReadOnlyList<ParameterDescriptor> GetParameters() =>
      _paramStore.GetParameters();

  public ParameterValue GetParameter(string key)
  {
    GrabberConfig config;
    lock (_lock) config = _config;
    return _paramStore.GetParameter(config, key);
  }

  public async Task SetParameterAsync(
      string key, ParameterValue value, CancellationToken ct = default)
  {
    // source_mode 변경
    if (_paramStore.ApplySourceModeParameter(key, value))
      return;

    switch (key)
    {
      case "wafer_image":
        ApplyWaferImage(value);
        return;

      case "scan_plan":
        ApplyScanPlan(value);
        return;

      case "acquisition_mode":
        await ApplyAcquisitionModeAsync(value, ct);
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

  public async Task<CommandResult> ExecuteCommandAsync(
      string command, CancellationToken ct = default)
  {
    switch (command)
    {
      case "reset_counter":
        Interlocked.Exchange(ref _frameCount, 0);
        lock (_lock) _currentShotIndex = 0;
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

  // ── 파라미터 적용 ────────────────────────────────────────────────────────

  private void ApplyWaferImage(ParameterValue value)
  {
    if (value is not ParameterValue.BytesValue bytes)
      throw new ArgumentException("Expected Bytes for 'wafer_image'");

    lock (_lock)
    {
      if (_state == CE.GrabberState.Acquiring)
        throw new InvalidOperationException("Cannot set wafer image while acquiring.");

      DecodeWaferImage(bytes.Value);
    }
  }

  private void ApplyScanPlan(ParameterValue value)
  {
    string jsonText = value switch
    {
      ParameterValue.StringValue s => s.Value,
      ParameterValue.BytesValue b  => System.Text.Encoding.UTF8.GetString(b.Value),
      _ => throw new ArgumentException("Expected String or Bytes (JSON) for 'scan_plan'"),
    };

    lock (_lock)
    {
      if (_state == CE.GrabberState.Acquiring)
        throw new InvalidOperationException("Cannot set scan plan while acquiring.");

      _scanPlan  = JsonSerializer.Deserialize<ScanPlan>(jsonText, JsonOptions)
                   ?? throw new ArgumentException("Failed to deserialize ScanPlan.");
      _flatShots = FlattenShots(_scanPlan);
      _currentShotIndex = 0;

      // ScanPlan FOV에서 GrabberConfig Width/Height를 자동 계산
      if (_waferImageData is not null)
        UpdateConfigFromScanPlan(_scanPlan);
    }
  }

  private async Task ApplyAcquisitionModeAsync(ParameterValue value, CancellationToken ct)
  {
    GrabberConfig newConfig;
    bool wasAcquiring;
    CancellationTokenSource? oldCts;

    lock (_lock)
    {
      newConfig    = _paramStore.ApplyParameter(_config, "acquisition_mode", value);
      wasAcquiring = _state == CE.GrabberState.Acquiring;
      oldCts       = _continuousCts;

      _config        = newConfig;
      _continuousCts = null;
    }

    if (oldCts is not null)
    {
      await oldCts.CancelAsync();
      oldCts.Dispose();
    }

    if (wasAcquiring && newConfig.Mode == CE.AcquisitionMode.Continuous)
    {
      lock (_lock)
      {
        _continuousCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = RunContinuousLoopAsync(newConfig, _channel, _continuousCts.Token);
      }
    }
  }

  // ── 이미지 디코딩 ────────────────────────────────────────────────────────

  /// <summary>
  /// 클라이언트에서 전달받은 원시 픽셀 데이터를 디코딩하여 저장한다.
  /// 현재는 RAW 바이트(Mono8) 가정. PNG 등 실제 디코딩은 추후 교체.
  /// </summary>
  private void DecodeWaferImage(byte[] rawData)
  {
    // TODO: SixLabors.ImageSharp 등으로 PNG/BMP 디코딩 교체.
    // 현재는 RAW Mono8 가정: 정사각형 이미지로 추정.
    int side = (int)Math.Sqrt(rawData.Length);
    if (side * side != rawData.Length)
    {
      side = rawData.Length;
      _waferImageWidth  = side;
      _waferImageHeight = 1;
    }
    else
    {
      _waferImageWidth  = side;
      _waferImageHeight = side;
    }

    _waferImageData        = rawData;
    _waferImagePixelFormat = CE.PixelFormat.Mono8;
    _waferImageStride      = _waferImageWidth * BytesPerPixel(_waferImagePixelFormat);
  }

  // ── Scan 모드: Shot에서 프레임 생성 (좌표 변환 + crop) ─────────────────────

  private GrabbedFrame CropShotFrame(ScanShot shot, CE.PixelFormat outputFormat, long frameIndex)
  {
    double radiusUm   = _scanPlan!.DieMap.RadiusUm;
    double diameterUm = radiusUm * 2.0;

    double scaleX = _waferImageWidth  / diameterUm;
    double scaleY = _waferImageHeight / diameterUm;

    // 이미지 관례: 원점 = 좌상단, Y↓ / 웨이퍼 좌표: 원점 = 중심, Y↑
    int cropLeft   = (int)Math.Floor((shot.LeftUm   + radiusUm) * scaleX);
    int cropTop    = (int)Math.Floor((radiusUm - shot.TopUm)    * scaleY);
    int cropWidth  = (int)Math.Ceiling(shot.Fov.WidthUm  * scaleX);
    int cropHeight = (int)Math.Ceiling(shot.Fov.HeightUm * scaleY);

    int srcBpp    = BytesPerPixel(_waferImagePixelFormat);
    int outBpp    = BytesPerPixel(outputFormat);
    int outStride = cropWidth * outBpp;
    var pixelData = new byte[outStride * cropHeight];

    for (int dy = 0; dy < cropHeight; dy++)
    {
      int srcY = cropTop + dy;
      if (srcY < 0 || srcY >= _waferImageHeight) continue;

      for (int dx = 0; dx < cropWidth; dx++)
      {
        int srcX = cropLeft + dx;
        if (srcX < 0 || srcX >= _waferImageWidth) continue;

        int srcIdx = srcY * _waferImageStride + srcX * srcBpp;
        int dstIdx = dy * outStride + dx * outBpp;

        byte lum = _waferImageData![srcIdx];

        if (outBpp == 1)
        {
          pixelData[dstIdx] = lum;
        }
        else
        {
          pixelData[dstIdx]     = lum;
          pixelData[dstIdx + 1] = lum;
          pixelData[dstIdx + 2] = lum;
        }
      }
    }

    return new GrabbedFrame(
        FrameId:     $"shot_{frameIndex:D8}",
        PixelData:   pixelData,
        Width:       cropWidth,
        Height:      cropHeight,
        PixelFormat: outputFormat,
        Stride:      outStride,
        Timestamp:   DateTimeOffset.UtcNow);
  }

  // ── Continuous 루프 ───────────────────────────────────────────────────────

  private async Task RunContinuousLoopAsync(
      GrabberConfig config, Channel<GrabbedFrame> channel, CancellationToken ct)
  {
    var interval = TimeSpan.FromSeconds(1.0 / config.FrameRateHz);
    using var timer = new PeriodicTimer(interval);

    try
    {
      while (await timer.WaitForNextTickAsync(ct))
      {
        GrabbedFrame frame;
        long index = Interlocked.Increment(ref _frameCount);

        if (IsScanMode)
        {
          ScanShot shot;
          bool allDone;

          lock (_lock)
          {
            if (_currentShotIndex >= _flatShots.Count)
            {
              _state = CE.GrabberState.Idle;
              break;
            }
            shot = _flatShots[_currentShotIndex];
          }

          frame = CropShotFrame(shot, config.PixelFormat, index);

          lock (_lock)
          {
            _currentShotIndex++;
            allDone = _currentShotIndex >= _flatShots.Count;
            if (allDone) _state = CE.GrabberState.Idle;
          }

          await channel.Writer.WriteAsync(frame, ct);
          if (allDone) break;
        }
        else
        {
          frame = _gradientSynthesizer.BuildFrame(config, index);
          await channel.Writer.WriteAsync(frame, ct);
        }
      }
    }
    catch (OperationCanceledException) { }
  }

  // ── 유틸리티 ──────────────────────────────────────────────────────────────

  private void ValidateScanReady()
  {
    if (_waferImageData is null)
      throw new InvalidOperationException("Wafer image is not set.");
    if (_scanPlan is null)
      throw new InvalidOperationException("ScanPlan is not set.");
    if (_flatShots.Count == 0)
      throw new InvalidOperationException("ScanPlan contains no shots.");
  }

  private void UpdateConfigFromScanPlan(ScanPlan plan)
  {
    double radiusUm   = plan.DieMap.RadiusUm;
    double diameterUm = radiusUm * 2.0;

    double scaleX = _waferImageWidth  / diameterUm;
    double scaleY = _waferImageHeight / diameterUm;

    int fovWidthPx  = (int)Math.Ceiling(plan.Fov.WidthUm  * scaleX);
    int fovHeightPx = (int)Math.Ceiling(plan.Fov.HeightUm * scaleY);

    _config = _config with { Width = fovWidthPx, Height = fovHeightPx };
  }

  private static IReadOnlyList<ScanShot> FlattenShots(ScanPlan plan)
  {
    var shots = new List<ScanShot>();
    foreach (var sector in plan.Sectors)
    {
      shots.AddRange(sector.Shots);
    }
    return shots.AsReadOnly();
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
}
