using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Services;


/// <summary>
/// 합성 프레임을 생성하는 목업 IFrameSource 구현.
///
/// 더블 버퍼링 전략:
///   - _writeBuffer: 배경 스레드 전용. UI 스레드는 절대 접근하지 않는다.
///   - _readBuffer:  UI 스레드 전용. 렌더러가 읽는 버퍼.
///   배경 스레드가 _writeBuffer 쓰기를 완료하면
///   Dispatcher.UIThread.Post 로 두 버퍼의 역할을 교체하고
///   FrameSwapped 이벤트를 발생시킨다.
///
/// 동기화:
///   _swapReady(AutoResetEvent)가 배경 스레드와 UI 스레드 사이의 게이트 역할을 한다.
///   - 배경 스레드: WaitOne() → RenderFrame() → Post(swap + Set())
///   - UI 스레드:   swap() → _swapReady.Set()
///   이 순서를 통해 이전 교체가 완료되기 전에 배경 스레드가
///   새 프레임을 쓰기 시작하는 레이스 컨디션을 방지한다.
///
/// 런타임 설정 변경:
///   - Fps:    volatile int — 배경 스레드가 루프마다 읽는다. lock 불필요.
///   - Width/Height: 변경 시 _resizePending 플래그를 세운다.
///                   배경 스레드는 WaitOne() 이후 플래그를 확인하고
///                   두 버퍼를 모두 안전하게 재할당한다.
/// </summary>
public sealed class MockFrameSourceService : IFrameSource, IFrameGrabberController
{
    public event EventHandler<WriteableBitmap>? FrameSwapped;


    private readonly ILogService _log;

    // ── 설정 프로퍼티 ─────────────────────────────────────────────────────

    private volatile int _fps;
    private int _width;
    private int _height;

    /// <summary>초당 프레임 수. 실행 중 변경 가능.</summary>
    public int Fps
    {
        get => _fps;
        set => _fps = Math.Max(1, value);
    }

    /// <summary>프레임 폭(픽셀). 실행 중 변경 시 다음 프레임부터 반영된다.</summary>
    public int Width
    {
        get => _width;
        set
        {
            if (_width == value) return;
            _width = Math.Max(1, value);
            _resizePending = true;
        }
    }

    /// <summary>프레임 높이(픽셀). 실행 중 변경 시 다음 프레임부터 반영된다.</summary>
    public int Height
    {
        get => _height;
        set
        {
            if (_height == value) return;
            _height = Math.Max(1, value);
            _resizePending = true;
        }
    }

    // ── 더블 버퍼 ────────────────────────────────────────────────────────
    // _writeBuffer: 배경 스레드 전용 (쓰기)
    // _readBuffer:  UI 스레드 전용 (읽기 / Source 노출)
    // 두 필드는 UI 스레드에서만 교체되므로 별도의 lock이 필요 없다.

    private WriteableBitmap _writeBuffer;
    private WriteableBitmap _readBuffer;

    // ── 내부 상태 ─────────────────────────────────────────────────────────

    private readonly Thread          _thread;
    private readonly AutoResetEvent  _swapReady = new(initialState: true);
    private volatile bool            _running;
    private volatile bool            _resizePending;
    private int                      _frameIndex;

    // ── 생성자 ────────────────────────────────────────────────────────────

    public MockFrameSourceService(ILogService logService, int fps = 60, int width = 1024, int height = 1024)
    {
        _log    = logService;
        _fps    = Math.Max(1, fps);
        _width  = Math.Max(1, width);
        _height = Math.Max(1, height);

        (_writeBuffer, _readBuffer) = AllocateBuffers(_width, _height);

        _thread = new Thread(ThreadLoop)
        {
            IsBackground = true,
            Name         = "MockFrameSource",
        };
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        // WaitOne()에서 블로킹 중인 배경 스레드가 루프 조건을 확인하고 정상 종료되도록 신호를 보낸다.
        _swapReady.Set();
    }

    private static readonly string[] SupportedProperties = ["ImageWidth", "ImageHeight", "ExposureUs"];

    public void SetProperty(string key, object? value)
    {
        switch (key)
        {
            case "ImageWidth" when value is int w:
                Width = w;
                _log.Info(this, $"SetProperty: {key} = {w} px");
                break;

            case "ImageHeight" when value is int h:
                Height = h;
                _log.Info(this, $"SetProperty: {key} = {h} px");
                break;

            case "ExposureUs" when value is int us and > 0:
                Fps = Math.Max(1, 1_000_000 / us);
                _log.Info(this, $"SetProperty: {key} = {us} μs → Fps = {Fps}");
                break;

            default:
                _log.Warning(this, $"SetProperty: '{key}' is not supported. Supported properties: {string.Join(", ", SupportedProperties)}");
                break;
        }
    }

  public Task StartAcquisitionAsync(CancellationToken ct = default) => Task.CompletedTask;
  public Task StopAcquisitionAsync(CancellationToken ct = default)  => Task.CompletedTask;
  public Task TriggerFrameAsync(CancellationToken ct = default)     => Task.CompletedTask;

  public Task<GrabberCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
      => Task.FromResult(GrabberCapabilities.Empty);

  public Task<object?> GetParameterAsync(string key, CancellationToken ct = default)
      => Task.FromResult<object?>(null);

  public Task ExecuteCommandAsync(string commandKey, CancellationToken ct = default)
      => Task.CompletedTask;

  public Task<(bool Success, string Message)> SetParameterAsync(
      string key, object? value, CancellationToken ct = default)
      => Task.FromResult((true, string.Empty));

    // ── 배경 스레드 루프 ──────────────────────────────────────────────────

    private void ThreadLoop()
    {
        while (_running)
        {
            // 이전 프레임의 버퍼 교체가 UI 스레드에서 완료될 때까지 대기한다.
            // _swapReady는 초기값 true이므로 첫 프레임은 대기 없이 통과한다.
            // Stop() 호출 시에도 Set()으로 깨어나 루프 조건에서 종료된다.
            _swapReady.WaitOne();
            if (!_running) break;

            // Width/Height 변경 요청: WaitOne() 통과 후에는 UI 스레드가 버퍼를 보지 않는 것이
            // 보장되므로 _readBuffer도 배경 스레드에서 직접 교체해도 안전하다.
            if (_resizePending)
            {
                _resizePending = false;
                (_writeBuffer, _readBuffer) = AllocateBuffers(_width, _height);
            }

            var sw       = Stopwatch.StartNew();
            var targetMs = 1000 / _fps;

            // 1. 배경 스레드에서 _writeBuffer에만 픽셀을 쓴다.
            RenderFrame(_writeBuffer);

            // 2. UI 스레드에서 버퍼 역할을 교체하고, 완료 신호를 보낸 뒤 이벤트를 발생시킨다.
            Dispatcher.UIThread.Post(() =>
            {
                (_readBuffer, _writeBuffer) = (_writeBuffer, _readBuffer);
                _swapReady.Set();
                FrameSwapped?.Invoke(this, _readBuffer);
            }, DispatcherPriority.Render);

            sw.Stop();
            var sleep = targetMs - (int)sw.ElapsedMilliseconds;
            if (sleep > 0)
                Thread.Sleep(sleep);
        }
    }

    // ── 버퍼 할당 ─────────────────────────────────────────────────────────

    private static (WriteableBitmap write, WriteableBitmap read) AllocateBuffers(int width, int height)
    {
        var pixelSize = new PixelSize(width, height);
        var dpi       = new Vector(96, 96);
        return (
            new WriteableBitmap(pixelSize, dpi, PixelFormat.Bgra8888, AlphaFormat.Opaque),
            new WriteableBitmap(pixelSize, dpi, PixelFormat.Bgra8888, AlphaFormat.Opaque));
    }

    // ── 프레임 렌더링 ─────────────────────────────────────────────────────

    private unsafe void RenderFrame(WriteableBitmap target)
    {
        var (r, g, b) = HsvToRgb((_frameIndex & 0xFF) / 255f, 1f, 0.65f);

        using var fb = target.Lock();

        var ptr    = (byte*)fb.Address;
        var stride = fb.RowBytes;
        var width  = fb.Size.Width;
        var height = fb.Size.Height;

        for (var y = 0; y < height; y++)
        {
            var row = ptr + y * stride;
            for (var x = 0; x < width; x++)
            {
                var p = row + x * 4;
                p[0] = b;   // B
                p[1] = g;   // G
                p[2] = r;   // R
                p[3] = 255; // A
            }
        }

        _frameIndex++;
    }

    // ── HSV → RGB 변환 ────────────────────────────────────────────────────

    /// <summary>h, s, v 모두 [0, 1] 범위.</summary>
    private static (byte r, byte g, byte b) HsvToRgb(float h, float s, float v)
    {
        int   i  = (int)(h * 6f);
        float f  = h * 6f - i;
        float p  = v * (1f - s);
        float q  = v * (1f - f * s);
        float t  = v * (1f - (1f - f) * s);

        var (rf, gf, bf) = (i % 6) switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };

        return ((byte)(rf * 255), (byte)(gf * 255), (byte)(bf * 255));
    }
}
