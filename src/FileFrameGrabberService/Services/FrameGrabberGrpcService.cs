using System.Threading.Channels;
using Core.Grpc.FrameGrabber;
using Core.FrameGrabber.Interfaces;
using Core.SharedMemory.Models;
using FileFrameGrabberService.Utils;
using Grpc.Core;

using ProtoCommandResult       = Core.Grpc.FrameGrabber.CommandResult;
using ProtoCommandDescriptor   = Core.Grpc.FrameGrabber.CommandDescriptor;
using ProtoParameterDescriptor = Core.Grpc.FrameGrabber.ParameterDescriptor;

namespace FileFrameGrabberService.Services;

public sealed class FrameGrabberGrpcService : FrameGrabber.FrameGrabberBase
{
  private readonly IFrameGrabber          _grabber;
  private readonly FramePumpHostedService _pump;
  private readonly ActiveStreamRegistry  _registry;
  private readonly ILogger<FrameGrabberGrpcService> _logger;

  public FrameGrabberGrpcService(
      IFrameGrabber                    grabber,
      FramePumpHostedService           pump,
      ActiveStreamRegistry             registry,
      ILogger<FrameGrabberGrpcService> logger)
  {
    _grabber  = grabber;
    _pump     = pump;
    _registry = registry;
    _logger   = logger;
  }

  // ── 획득 제어 RPCs ────────────────────────────────────────────────────────

  public override async Task<ConfigureResponse> Configure(
      ConfigureRequest request, ServerCallContext context)
  {
    try
    {
      var config = new Core.FrameGrabber.Models.GrabberConfig(
          Mode:        FrameGrabberProtoMapper.ToMode(request.Mode),
          PixelFormat: FrameGrabberProtoMapper.ToPixelFormat(request.PixelFormat),
          Width:       request.Width       > 0 ? request.Width       : Core.FrameGrabber.Models.GrabberConfig.Default.Width,
          Height:      request.Height      > 0 ? request.Height      : Core.FrameGrabber.Models.GrabberConfig.Default.Height,
          FrameRateHz: request.FrameRateHz > 0 ? request.FrameRateHz : Core.FrameGrabber.Models.GrabberConfig.Default.FrameRateHz);

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
      _logger.LogInformation("StartAcquisition requested");
      await _grabber.StartAsync(context.CancellationToken);
      _pump.StartPump();
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
      _logger.LogInformation("StopAcquisition requested");
      await _pump.StopPumpAsync();
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
  /// TriggerAsync()로 IFrameGrabber 채널에 프레임을 밀어 넣고,
  /// FramePump가 링버퍼에 기록한 뒤 발행하는 FrameWritten 이벤트를 1회 수신한다.
  /// </summary>
  public override async Task<FrameHandle> TriggerFrame(
      TriggerFrameRequest request, ServerCallContext context)
  {
    var tcs = new TaskCompletionSource<FrameInfo>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    void Handler(FrameInfo info)
    {
      _pump.FrameWritten -= Handler;
      tcs.TrySetResult(info);
    }

    _pump.FrameWritten += Handler;

    try
    {
      _logger.LogInformation("TriggerFrame requested");
      await _grabber.TriggerAsync(context.CancellationToken);
      var info = await tcs.Task.WaitAsync(context.CancellationToken);
      _logger.LogInformation("TriggerFrame completed: frameId={FrameId} slot={Slot} seq={Seq}",
          info.FrameId, info.SlotIndex, info.Sequence);
      return FrameGrabberProtoMapper.ToProtoHandle(info);
    }
    catch
    {
      _pump.FrameWritten -= Handler;
      throw;
    }
  }

  /// <summary>
  /// FramePump가 링버퍼에 프레임을 기록할 때마다 FrameHandle을 스트리밍한다.
  /// 클라이언트 연결이 끊기면 이벤트 구독을 해제한다.
  /// </summary>
  public override async Task SubscribeFrames(
      SubscribeFramesRequest           request,
      IServerStreamWriter<FrameHandle> responseStream,
      ServerCallContext                context)
  {
    _logger.LogInformation("SubscribeFrames started");

    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
    using var _registration = _registry.Register(linkedCts);

    var channel = Channel.CreateBounded<FrameInfo>(
        new BoundedChannelOptions(32)
        {
          FullMode     = BoundedChannelFullMode.DropOldest,
          SingleReader = true,
          SingleWriter = false
        });

    void Handler(FrameInfo info) => channel.Writer.TryWrite(info);
    _pump.FrameWritten += Handler;

    try
    {
      await foreach (var info in channel.Reader.ReadAllAsync(linkedCts.Token))
      {
        await responseStream.WriteAsync(FrameGrabberProtoMapper.ToProtoHandle(info), linkedCts.Token);
        _logger.LogDebug("SubscribeFrames → {FrameId} slot={Slot} seq={Seq}",
            info.FrameId, info.SlotIndex, info.Sequence);
      }
    }
    catch (OperationCanceledException)
    {
      // 클라이언트 연결 종료 또는 앱 종료 — 정상 종료 경로
    }
    finally
    {
      _pump.FrameWritten -= Handler;
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
      State         = FrameGrabberProtoMapper.ToProtoState(s.State),
      Mode          = FrameGrabberProtoMapper.ToProtoMode(s.Mode),
      FramesGrabbed = s.FramesGrabbed,
      Message       = s.Message ?? string.Empty
    });
  }

  // ── 동적 파라미터 / 명령 RPCs ─────────────────────────────────────────────

  public override Task<CapabilitiesResponse> GetCapabilities(
      GetCapabilitiesRequest request, ServerCallContext context)
  {
    var response = new CapabilitiesResponse();

    foreach (var p in _grabber.GetParameters())
    {
      var descriptor = new ProtoParameterDescriptor
      {
        Key         = p.Key,
        DisplayName = p.DisplayName,
        ValueType   = FrameGrabberProtoMapper.ToProtoParameterValueType(p.ValueType),
      };
      if (p.MinValue     is not null) descriptor.MinValue     = FrameGrabberProtoMapper.ToProtoParameterValueFromRaw(p.MinValue, p.ValueType);
      if (p.MaxValue     is not null) descriptor.MaxValue     = FrameGrabberProtoMapper.ToProtoParameterValueFromRaw(p.MaxValue, p.ValueType);
      if (p.DefaultValue is not null) descriptor.DefaultValue = FrameGrabberProtoMapper.ToProtoParameterValueFromRaw(p.DefaultValue, p.ValueType);
      response.Parameters.Add(descriptor);
    }

    foreach (var c in _grabber.GetCommands())
    {
      response.Commands.Add(new ProtoCommandDescriptor
      {
        Key         = c.Key,
        DisplayName = c.DisplayName,
        Description = c.Description ?? string.Empty
      });
    }

    return Task.FromResult(response);
  }

  public override Task<GetParameterResponse> GetParameter(
      GetParameterRequest request, ServerCallContext context)
  {
    try
    {
      var value = _grabber.GetParameter(request.Key);
      return Task.FromResult(new GetParameterResponse
      {
        Key   = request.Key,
        Value = FrameGrabberProtoMapper.ToProtoValue(value)
      });
    }
    catch (KeyNotFoundException ex)
    {
      throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
    }
  }

  public override async Task<SetParameterResponse> SetParameter(
      SetParameterRequest request, ServerCallContext context)
  {
    try
    {
      var value = FrameGrabberProtoMapper.ToDomainValue(request.Value);
      await _grabber.SetParameterAsync(request.Key, value, context.CancellationToken);
      _logger.LogInformation("SetParameter: key={Key} value={Value}", request.Key, value);
      return new SetParameterResponse { Success = true };
    }
    catch (KeyNotFoundException ex)
    {
      return new SetParameterResponse { Success = false, Message = ex.Message };
    }
    catch (ArgumentException ex)
    {
      return new SetParameterResponse { Success = false, Message = ex.Message };
    }
    catch (InvalidOperationException ex)
    {
      return new SetParameterResponse { Success = false, Message = ex.Message };
    }
  }

  public override async Task<ProtoCommandResult> ExecuteCommand(
      ExecuteCommandRequest request, ServerCallContext context)
  {
    try
    {
      var result = await _grabber.ExecuteCommandAsync(request.Command, context.CancellationToken);
      _logger.LogInformation("ExecuteCommand: {Command} success={Success}", request.Command, result.Success);
      return FrameGrabberProtoMapper.ToProtoCommandResult(result);
    }
    catch (KeyNotFoundException ex)
    {
      throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
    }
  }
}
