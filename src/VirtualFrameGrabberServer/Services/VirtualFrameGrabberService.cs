using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Core.FrameGrabber.Interfaces;
using Core.FrameGrabber.Models;
using Core.Models;
using OpenCvSharp;
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

  private byte[]?        _dieImageData;
  private int            _dieImageWidth;
  private int            _dieImageHeight;
  private int            _dieImageStride;

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
        if (_scanPlan is null || _dieImageData is null)
          throw new InvalidOperationException(
              "Die image and ScanPlan must be set before triggering.");

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
      case "die_image":
        ApplyDieImage(value);
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

  private void ApplyDieImage(ParameterValue value)
  {
    if (value is not ParameterValue.BytesValue bytes)
      throw new ArgumentException("Expected Bytes for 'die_image'");

    lock (_lock)
    {
      if (_state == CE.GrabberState.Acquiring)
        throw new InvalidOperationException("Cannot set die image while acquiring.");

      DecodeDieImage(bytes.Value);

      if (_scanPlan is not null)
        UpdateConfigFromDieImage(_scanPlan);
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

      if (_dieImageData is not null)
        UpdateConfigFromDieImage(_scanPlan);
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
  /// 클라이언트에서 전달받은 인코딩된 Die 이미지(PNG/JPEG)를 디코딩하여 Mono8로 저장한다.
  /// OpenCvSharp의 ImDecode를 사용하여 이미지 포맷에 내장된 차원 정보를 자동으로 추출한다.
  /// </summary>
  private void DecodeDieImage(byte[] encodedData)
  {
    using var mat = Cv2.ImDecode(encodedData, ImreadModes.Grayscale);
    if (mat.Empty())
    {
      throw new ArgumentException("Die 이미지 디코딩에 실패했습니다. 지원되는 이미지 포맷(PNG/JPEG)인지 확인하십시오.");
    }

    _dieImageWidth  = mat.Width;
    _dieImageHeight = mat.Height;

    int stride = mat.Width;
    var pixels = new byte[stride * mat.Height];
    unsafe
    {
      var src = (byte*)mat.DataPointer;
      for (int y = 0; y < mat.Height; y++)
      {
        var srcRow = new ReadOnlySpan<byte>(src + y * mat.Step(), mat.Width);
        srcRow.CopyTo(pixels.AsSpan(y * stride, mat.Width));
      }
    }

    _dieImageData  = pixels;
    _dieImageStride = stride;
  }

  // ── Scan 모드: Shot FOV를 카메라 해상도로 렌더링 ────────────────────────

  /// <summary>
  /// Shot의 FOV 영역에 해당하는 프레임을 카메라 해상도(Config.Width × Height)로 생성한다.
  /// FOV 내 각 픽셀의 물리 좌표를 계산하고, 해당 위치의 Die 이미지를 리샘플링하여 채운다.
  /// Die 영역 밖의 픽셀은 검정(0)으로 남는다.
  /// </summary>
  private GrabbedFrame CropShotFrame(ScanShot shot, CE.PixelFormat outputFormat, long frameIndex)
  {
    int outW = _config.Width;
    int outH = _config.Height;
    int outBpp    = BytesPerPixel(outputFormat);
    int outStride = outW * outBpp;
    var pixelData = new byte[outStride * outH];

    var dieMap = _scanPlan!.DieMap;

    // Die 물리 크기 (모든 Die는 동일 크기)
    var firstDie  = dieMap.Dies[0];
    double dieWum = firstDie.WidthUm;
    double dieHum = firstDie.HeightUm;

    // Shot이 겹치는 Die만 수집 (전체 DieMap 탐색 대신)
    var coveredDies = new List<DieRegion>(shot.CoveredDies.Count);
    foreach (var idx in shot.CoveredDies)
    {
      var region = dieMap.FindByIndex(idx);
      if (region is not null) coveredDies.Add(region.Value);
    }

    // Die 이미지(px) ↔ Die 물리(µm) 스케일
    double dieScaleX = _dieImageWidth  / dieWum;
    double dieScaleY = _dieImageHeight / dieHum;

    // 출력 프레임(px) ↔ FOV 물리(µm) 스케일
    double umPerPxX = shot.Fov.WidthUm  / outW;
    double umPerPxY = shot.Fov.HeightUm / outH;

    for (int py = 0; py < outH; py++)
    {
      // 출력 픽셀 → 웨이퍼 물리 좌표 (이미지 Y↓, 웨이퍼 Y↑)
      double waferY = shot.TopUm - (py + 0.5) * umPerPxY;

      for (int px = 0; px < outW; px++)
      {
        double waferX = shot.LeftUm + (px + 0.5) * umPerPxX;

        // CoveredDies만 탐색 (보통 1~4개)
        DieRegion? found = null;
        foreach (var die in coveredDies)
        {
          if (waferX >= die.BottomLeft.Xum && waferX <= die.BottomLeft.Xum + die.WidthUm &&
              waferY >= die.BottomLeft.Yum && waferY <= die.BottomLeft.Yum + die.HeightUm)
          {
            found = die;
            break;
          }
        }
        if (found is null) continue;

        // 웨이퍼 좌표 → Die 로컬 좌표 (Die BottomLeft 기준)
        double dieLocalX = waferX - found.Value.BottomLeft.Xum;
        double dieLocalY = waferY - found.Value.BottomLeft.Yum;

        // Die 로컬 좌표 → Die 이미지 픽셀 (이미지 Y↓ 변환)
        int srcX = (int)(dieLocalX * dieScaleX);
        int srcY = (int)((dieHum - dieLocalY) * dieScaleY);

        if (srcX < 0 || srcX >= _dieImageWidth ||
            srcY < 0 || srcY >= _dieImageHeight) continue;

        byte lum = _dieImageData![srcY * _dieImageStride + srcX];
        int dstIdx = py * outStride + px * outBpp;

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
        Width:       outW,
        Height:      outH,
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

  /// <summary>
  /// Die 이미지 해상도와 Die 물리 크기로부터 픽셀/µm 비율을 구하고,
  /// FOV 크기에 맞춰 GrabberConfig의 Width/Height를 갱신한다.
  /// 출력 프레임 1px = Die 이미지 1px이 되도록 보장한다.
  /// </summary>
  private void UpdateConfigFromDieImage(ScanPlan plan)
  {
    var firstDie = plan.DieMap.Dies[0];
    double scaleX = _dieImageWidth  / firstDie.WidthUm;
    double scaleY = _dieImageHeight / firstDie.HeightUm;

    int fovWidthPx  = (int)Math.Ceiling(plan.Fov.WidthUm  * scaleX);
    int fovHeightPx = (int)Math.Ceiling(plan.Fov.HeightUm * scaleY);

    _config = _config with { Width = fovWidthPx, Height = fovHeightPx };
  }

  private void ValidateScanReady()
  {
    if (_dieImageData is null)
      throw new InvalidOperationException("Die image is not set.");
    if (_scanPlan is null)
      throw new InvalidOperationException("ScanPlan is not set.");
    if (_flatShots.Count == 0)
      throw new InvalidOperationException("ScanPlan contains no shots.");
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
