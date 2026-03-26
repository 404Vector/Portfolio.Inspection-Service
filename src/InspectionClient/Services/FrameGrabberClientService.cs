using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Core.Grpc.FrameGrabber;
using Core.Logging.Interfaces;
using Core.SharedMemory.Enums;
using Core.SharedMemory.Models;
using Core.SharedMemory.Reader;
using Grpc.Core;
using InspectionClient.Interfaces;

using GrpcFrameGrabber  = Core.Grpc.FrameGrabber.FrameGrabber;
using ProtoPixelFormat  = Core.Grpc.FrameGrabber.PixelFormat;
using DomainPixelFormat = Core.Enums.PixelFormat;

namespace InspectionClient.Services;

/// <summary>
/// FileFrameGrabberService gRPC를 통해 프레임을 수신하는 IFrameSource 구현.
///
/// 더블 버퍼링 전략 (MockFrameSourceService와 동일):
///   - _writeBuffer: 배경 스레드 전용. UI 스레드는 절대 접근하지 않는다.
///   - _readBuffer:  UI 스레드 전용. 렌더러가 읽는 버퍼.
///   배경 스레드가 _writeBuffer 쓰기를 완료하면
///   Dispatcher.UIThread.Post 로 두 버퍼의 역할을 교체하고
///   FrameSwapped 이벤트를 발생시킨다.
///
/// 프레임 수신 파이프라인:
///   [Pump Task (Thread Pool)]
///     gRPC SubscribeFrames 스트림을 읽어 BoundedChannel에 Write.
///     연결 실패 시 2초 대기 후 재시도.
///
///   [Background Thread: SubscriptionLoop]
///     Channel에서 FrameHandle을 읽어 SharedMemoryRingBufferReader로
///     픽셀 데이터를 복사한 뒤 _writeBuffer에 기록.
///
/// SetProperty:
///   OpticSettings 프로퍼티명을 키로 받아 gRPC SetParameter를 fire-and-forget 호출.
///   ImageWidth/ImageHeight는 로컬 버퍼 리사이즈도 트리거한다.
/// </summary>
public sealed class FrameGrabberClientService : IFrameSource, IDisposable
{
  public event EventHandler<WriteableBitmap>? FrameSwapped;

  // ── 의존성 ─────────────────────────────────────────────────────────────

  private readonly GrpcFrameGrabber.FrameGrabberClient _grpcClient;
  private readonly ILogService _log;

  // ── 더블 버퍼 ────────────────────────────────────────────────────────

  private WriteableBitmap _writeBuffer;
  private WriteableBitmap _readBuffer;
  private readonly AutoResetEvent _swapReady = new(initialState: true);

  // ── 구독 생명주기 ─────────────────────────────────────────────────────

  private Thread? _thread;
  private CancellationTokenSource? _cts;
  private volatile bool _running;

  // ── 리사이즈 상태 ──────────────────────────────────────────────────────

  private volatile bool _resizePending;
  private int _width;
  private int _height;

  // ── 생성자 ────────────────────────────────────────────────────────────

  public FrameGrabberClientService(
      GrpcFrameGrabber.FrameGrabberClient grpcClient,
      ILogService logService,
      int width = 1024,
      int height = 1024)
  {
    _grpcClient = grpcClient;
    _log        = logService;
    _width      = Math.Max(1, width);
    _height     = Math.Max(1, height);

    (_writeBuffer, _readBuffer) = AllocateBuffers(_width, _height);
  }

  // ── IFrameSource ──────────────────────────────────────────────────────

  public void Start()
  {
    if (_running) return;
    _running = true;

    _cts    = new CancellationTokenSource();
    _thread = new Thread(SubscriptionLoop)
    {
      IsBackground = true,
      Name         = "FrameGrabberClient",
    };
    _thread.Start();
  }

  public void Stop()
  {
    _running = false;
    _cts?.Cancel();
    // WaitOne()에서 블로킹 중인 배경 스레드가 정상 종료되도록 신호를 보낸다.
    _swapReady.Set();
  }

  public void SetProperty(string key, object? value)
  {
    switch (key)
    {
      case "ImageWidth" when value is int w:
        _width         = Math.Max(1, w);
        _resizePending = true;
        break;
      case "ImageHeight" when value is int h:
        _height        = Math.Max(1, h);
        _resizePending = true;
        break;
    }

    var proto = ToParameterValue(value);
    var ct    = _cts?.Token ?? CancellationToken.None;

    _ = Task.Run(async () =>
    {
      try
      {
        var response = await _grpcClient.SetParameterAsync(
            new SetParameterRequest { Key = key, Value = proto },
            cancellationToken: ct);

        if (!response.Success)
          _log.Warning(this, $"SetParameter '{key}' failed: {response.Message}");
      }
      catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
      {
        // 정상 종료 경로 — 무시
      }
      catch (Exception ex)
      {
        _log.Warning(this, $"SetParameter '{key}' error: {ex.Message}");
      }
    }, ct);
  }

  // ── 배경 스레드 루프 ──────────────────────────────────────────────────

  private void SubscriptionLoop()
  {
    var ct = _cts!.Token;

    var channel = Channel.CreateBounded<FrameHandle>(
        new BoundedChannelOptions(32)
        {
          FullMode     = BoundedChannelFullMode.DropOldest,
          SingleReader = true,
          SingleWriter = true,
        });

    // gRPC 스트림 → Channel 펌프 (Thread Pool)
    _ = Task.Run(() => PumpAsync(channel.Writer, ct), ct);

    var readers = new Dictionary<string, SharedMemoryRingBufferReader>();

    try
    {
      while (_running)
      {
        // 이전 프레임의 버퍼 교체가 UI 스레드에서 완료될 때까지 대기한다.
        _swapReady.WaitOne();
        if (!_running) break;

        if (_resizePending)
        {
          _resizePending = false;
          (_writeBuffer, _readBuffer) = AllocateBuffers(_width, _height);
        }

        FrameHandle handle;
        try
        {
          handle = channel.Reader.ReadAsync(ct).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
          break;
        }

        var info = ToFrameInfo(handle);

        if (info.SizeBytes <= 0)
        {
          _swapReady.Set();
          continue;
        }

        // 같은 SharedMemoryKey라면 reader 재사용
        if (!readers.TryGetValue(info.SharedMemoryKey, out var reader))
        {
          try
          {
            reader = new SharedMemoryRingBufferReader(info.SharedMemoryKey);
            readers[info.SharedMemoryKey] = reader;
          }
          catch (Exception ex)
          {
            _log.Warning(this, $"MMF open failed '{info.SharedMemoryKey}': {ex.Message}");
            _swapReady.Set();
            continue;
          }
        }

        var pixels = new byte[info.SizeBytes];
        var result = reader.TryRead(info, pixels);

        if (result == ReadResult.Overwritten)
        {
          _log.Warning(this, $"Frame overwritten: seq={info.Sequence} slot={info.SlotIndex}");
          _swapReady.Set();
          continue;
        }

        if (result != ReadResult.Ok)
        {
          _swapReady.Set();
          continue;
        }

        // FrameHandle의 해상도가 현재 버퍼와 다르면 다음 루프에서 재할당
        if (handle.Width != _writeBuffer.PixelSize.Width ||
            handle.Height != _writeBuffer.PixelSize.Height)
        {
          _width         = handle.Width;
          _height        = handle.Height;
          _resizePending = true;
          _swapReady.Set();
          continue;
        }

        CopyFrameToBuffer(pixels, handle.PixelFormat, _writeBuffer);

        Dispatcher.UIThread.Post(() =>
        {
          (_readBuffer, _writeBuffer) = (_writeBuffer, _readBuffer);
          _swapReady.Set();
          FrameSwapped?.Invoke(this, _readBuffer);
        }, DispatcherPriority.Render);
      }
    }
    finally
    {
      foreach (var r in readers.Values)
        r.Dispose();

      channel.Writer.TryComplete();
    }
  }

  // ── gRPC 스트림 펌프 ─────────────────────────────────────────────────

  private async Task PumpAsync(
      ChannelWriter<FrameHandle> writer,
      CancellationToken ct)
  {
    while (!ct.IsCancellationRequested)
    {
      try
      {
        using var call = _grpcClient.SubscribeFrames(
            new SubscribeFramesRequest(),
            cancellationToken: ct);

        await foreach (var handle in call.ResponseStream.ReadAllAsync(ct))
          writer.TryWrite(handle);
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
      {
        _log.Warning(this, "FileFrameGrabberService unavailable. Retrying in 2s...");
        try { await Task.Delay(2000, ct); }
        catch (OperationCanceledException) { break; }
      }
      catch (Exception ex)
      {
        _log.Warning(this, $"SubscribeFrames error: {ex.Message}");
        try { await Task.Delay(2000, ct); }
        catch (OperationCanceledException) { break; }
      }
    }

    writer.TryComplete();
  }

  // ── 버퍼 할당 ─────────────────────────────────────────────────────────

  private static (WriteableBitmap write, WriteableBitmap read) AllocateBuffers(int width, int height)
  {
    var pixelSize = new PixelSize(width, height);
    var dpi       = new Vector(96, 96);
    return (
        new WriteableBitmap(pixelSize, dpi, Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Opaque),
        new WriteableBitmap(pixelSize, dpi, Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Opaque));
  }

  // ── 픽셀 복사 ─────────────────────────────────────────────────────────

  private static unsafe void CopyFrameToBuffer(
      byte[] pixels,
      ProtoPixelFormat fmt,
      WriteableBitmap target)
  {
    using var fb = target.Lock();

    var dst    = (byte*)fb.Address;
    var dstStride = fb.RowBytes;
    var width  = fb.Size.Width;
    var height = fb.Size.Height;

    switch (fmt)
    {
      case ProtoPixelFormat.Mono8:
      {
        var srcStride = width; // Mono8: 1 byte/pixel
        for (var y = 0; y < height; y++)
        {
          var srcRow = pixels.AsSpan(y * srcStride, srcStride);
          var dstRow = dst + y * dstStride;
          for (var x = 0; x < width; x++)
          {
            var v  = srcRow[x];
            var p  = dstRow + x * 4;
            p[0]   = v;   // B
            p[1]   = v;   // G
            p[2]   = v;   // R
            p[3]   = 255; // A
          }
        }
        break;
      }

      case ProtoPixelFormat.Rgb8:
      {
        var srcStride = width * 3; // RGB8: 3 bytes/pixel
        for (var y = 0; y < height; y++)
        {
          var srcRow = pixels.AsSpan(y * srcStride, srcStride);
          var dstRow = dst + y * dstStride;
          for (var x = 0; x < width; x++)
          {
            var p  = dstRow + x * 4;
            p[0]   = srcRow[x * 3 + 2]; // B ← src R↔B swap
            p[1]   = srcRow[x * 3 + 1]; // G
            p[2]   = srcRow[x * 3 + 0]; // R
            p[3]   = 255;               // A
          }
        }
        break;
      }

      case ProtoPixelFormat.Bgr8:
      default:
      {
        var srcStride = width * 3; // BGR8: 3 bytes/pixel
        for (var y = 0; y < height; y++)
        {
          var srcRow = pixels.AsSpan(y * srcStride, srcStride);
          var dstRow = dst + y * dstStride;
          for (var x = 0; x < width; x++)
          {
            var p  = dstRow + x * 4;
            p[0]   = srcRow[x * 3 + 0]; // B
            p[1]   = srcRow[x * 3 + 1]; // G
            p[2]   = srcRow[x * 3 + 2]; // R
            p[3]   = 255;               // A
          }
        }
        break;
      }
    }
  }

  // ── 변환 헬퍼 ────────────────────────────────────────────────────────

  private static FrameInfo ToFrameInfo(FrameHandle h) => new(
      FrameId:         h.FrameId,
      SlotIndex:       h.SlotIndex,
      SharedMemoryKey: h.SharedMemoryKey,
      TimestampUs:     h.TimestampUs,
      Width:           h.Width,
      Height:          h.Height,
      PixelFormat:     ToPixelFormat(h.PixelFormat),
      Stride:          h.Stride,
      SizeBytes:       h.SizeBytes,
      Sequence:        h.Sequence);

  private static DomainPixelFormat ToPixelFormat(ProtoPixelFormat fmt) => fmt switch
  {
    ProtoPixelFormat.Rgb8 => DomainPixelFormat.Rgb8,
    ProtoPixelFormat.Bgr8 => DomainPixelFormat.Bgr8,
    _                     => DomainPixelFormat.Mono8,
  };

  private static ParameterValue ToParameterValue(object? value) => value switch
  {
    long   l => new ParameterValue { IntVal    = l },
    int    i => new ParameterValue { IntVal    = i },
    double d => new ParameterValue { DoubleVal = d },
    bool   b => new ParameterValue { BoolVal   = b },
    Enum   e => new ParameterValue { StringVal = e.ToString() },
    _        => new ParameterValue { StringVal = value?.ToString() ?? string.Empty },
  };

  // ── IDisposable ───────────────────────────────────────────────────────

  public void Dispose()
  {
    Stop();
    _cts?.Dispose();
    _swapReady.Dispose();
    _writeBuffer.Dispose();
    _readBuffer.Dispose();
  }
}
