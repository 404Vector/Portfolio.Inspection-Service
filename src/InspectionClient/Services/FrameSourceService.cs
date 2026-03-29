using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Core.Grpc.FrameGrabber;
using Core.Logging.Interfaces;
using Core.SharedMemory.Enums;
using Core.SharedMemory.Reader;
using Grpc.Core;
using InspectionClient.Interfaces;
using InspectionClient.Utils;

using GrpcFrameGrabber = Core.Grpc.FrameGrabber.FrameGrabber;

namespace InspectionClient.Services;

public sealed class FrameSourceService : IFrameSource, IDisposable
{
  public event EventHandler<WriteableBitmap>? FrameSwapped;

  private readonly GrpcFrameGrabber.FrameGrabberClient _grpcClient;
  private readonly ILogService _log;

  private WriteableBitmap _writeBuffer;
  private WriteableBitmap _readBuffer;
  private readonly AutoResetEvent _swapReady = new(initialState: true);

  private Thread? _thread;
  private CancellationTokenSource? _cts;
  private volatile bool _running;

  private volatile bool _resizePending;
  private int _width;
  private int _height;

  public FrameSourceService(
      GrpcFrameGrabber.FrameGrabberClient grpcClient,
      ILogService logService,
      int width = 1024,
      int height = 1024)
  {
    _grpcClient = grpcClient;
    _log        = logService;
    _width      = Math.Max(1, width);
    _height     = Math.Max(1, height);

    (_writeBuffer, _readBuffer) = FramePixelConverter.AllocateBuffers(_width, _height);
  }

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
  }

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

    _ = Task.Run(() => PumpAsync(channel.Writer, ct), ct);

    var readers = new Dictionary<string, SharedMemoryRingBufferReader>();
    try
    {
      while (_running)
      {
        _swapReady.WaitOne();
        if (!_running) break;

        if (_resizePending)
        {
          _resizePending = false;
          (_writeBuffer, _readBuffer) = FramePixelConverter.AllocateBuffers(_width, _height);
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

        var info = GrabberProtoMapper.ToFrameInfo(handle);

        if (info.SizeBytes <= 0)
        {
          _swapReady.Set();
          continue;
        }

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

        if (handle.Width != _writeBuffer.PixelSize.Width ||
            handle.Height != _writeBuffer.PixelSize.Height)
        {
          _width         = handle.Width;
          _height        = handle.Height;
          _resizePending = true;
          _swapReady.Set();
          continue;
        }

        FramePixelConverter.CopyFrameToBuffer(pixels, handle.PixelFormat, _writeBuffer);

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

  public void Dispose()
  {
    Stop();
    _cts?.Dispose();
    _swapReady.Dispose();
    _writeBuffer.Dispose();
    _readBuffer.Dispose();
  }
}
