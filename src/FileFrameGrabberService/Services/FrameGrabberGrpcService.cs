using System.Threading.Channels;
using Core.Grpc.FrameGrabber;
using Grpc.Core;
using Core.FrameGrabber.Interfaces;
using Core.SharedMemory.Models;
using Core.SharedMemory.Writer;

using DomainAcquisitionMode   = Core.Enums.AcquisitionMode;
using DomainPixelFormat        = Core.Enums.PixelFormat;
using DomainGrabberState       = Core.Enums.GrabberState;
using DomainParameterValue     = Core.FrameGrabber.Models.ParameterValue;
using DomainParameterValueType = Core.FrameGrabber.Models.ParameterValueType;
using DomainCommandResult      = Core.FrameGrabber.Models.CommandResult;

using ProtoParameterValue      = Core.Grpc.FrameGrabber.ParameterValue;
using ProtoCommandResult       = Core.Grpc.FrameGrabber.CommandResult;
using ProtoParameterDescriptor = Core.Grpc.FrameGrabber.ParameterDescriptor;
using ProtoCommandDescriptor   = Core.Grpc.FrameGrabber.CommandDescriptor;
using ProtoParameterValueType  = Core.Grpc.FrameGrabber.ParameterValueType;

namespace FileFrameGrabberService.Services;

public sealed class FrameGrabberGrpcService : FrameGrabber.FrameGrabberBase
{
  private readonly IFrameGrabber          _grabber;
  private readonly FramePumpHostedService _pump;
  private readonly ILogger<FrameGrabberGrpcService> _logger;

  public FrameGrabberGrpcService(
      IFrameGrabber                    grabber,
      FramePumpHostedService           pump,
      ILogger<FrameGrabberGrpcService> logger)
  {
    _grabber = grabber;
    _pump    = pump;
    _logger  = logger;
  }

  // ── 획득 제어 RPCs ────────────────────────────────────────────────────────

  public override async Task<ConfigureResponse> Configure(
      ConfigureRequest request, ServerCallContext context)
  {
    try
    {
      var config = new Core.FrameGrabber.Models.GrabberConfig(
          Mode:        ToMode(request.Mode),
          PixelFormat: ToPixelFormat(request.PixelFormat),
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
      await _grabber.TriggerAsync(context.CancellationToken);
      var info = await tcs.Task.WaitAsync(context.CancellationToken);
      _logger.LogDebug("TriggerFrame → {FrameId} slot={Slot} seq={Seq}",
          info.FrameId, info.SlotIndex, info.Sequence);
      return ToProtoHandle(info);
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
      await foreach (var info in channel.Reader.ReadAllAsync(context.CancellationToken))
      {
        await responseStream.WriteAsync(ToProtoHandle(info), context.CancellationToken);
        _logger.LogDebug("SubscribeFrames → {FrameId} slot={Slot} seq={Seq}",
            info.FrameId, info.SlotIndex, info.Sequence);
      }
    }
    catch (OperationCanceledException)
    {
      // 클라이언트 연결 종료 — 정상 종료 경로
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
      State         = ToProtoState(s.State),
      Mode          = ToProtoMode(s.Mode),
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
        ValueType   = ToProtoParameterValueType(p.ValueType),
      };
      if (p.MinValue     is not null) descriptor.MinValue     = ToProtoParameterValueFromRaw(p.MinValue, p.ValueType);
      if (p.MaxValue     is not null) descriptor.MaxValue     = ToProtoParameterValueFromRaw(p.MaxValue, p.ValueType);
      if (p.DefaultValue is not null) descriptor.DefaultValue = ToProtoParameterValueFromRaw(p.DefaultValue, p.ValueType);
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
        Value = ToProtoValue(value)
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
      var value = ToDomainValue(request.Value);
      await _grabber.SetParameterAsync(request.Key, value, context.CancellationToken);
      _logger.LogInformation("SetParameter: {Key}", request.Key);
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
      return ToProtoCommandResult(result);
    }
    catch (KeyNotFoundException ex)
    {
      throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
    }
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

  private static ProtoParameterValueType ToProtoParameterValueType(DomainParameterValueType t) => t switch
  {
    DomainParameterValueType.Int64  => ProtoParameterValueType.Int64,
    DomainParameterValueType.Double => ProtoParameterValueType.Double,
    DomainParameterValueType.Bool   => ProtoParameterValueType.Bool,
    DomainParameterValueType.String => ProtoParameterValueType.String,
    _                               => ProtoParameterValueType.Unspecified
  };

  /// <summary>
  /// ParameterDescriptor의 MinValue/MaxValue/DefaultValue(object?)를 proto ParameterValue로 변환.
  /// </summary>
  private static ProtoParameterValue ToProtoParameterValueFromRaw(object rawValue, DomainParameterValueType type)
  {
    var proto = new ProtoParameterValue();
    switch (type)
    {
      case DomainParameterValueType.Int64:  proto.IntVal    = Convert.ToInt64(rawValue);   break;
      case DomainParameterValueType.Double: proto.DoubleVal = Convert.ToDouble(rawValue);  break;
      case DomainParameterValueType.Bool:   proto.BoolVal   = Convert.ToBoolean(rawValue); break;
      case DomainParameterValueType.String: proto.StringVal = rawValue.ToString() ?? string.Empty; break;
    }
    return proto;
  }

  private static ProtoParameterValue ToProtoValue(DomainParameterValue v) => v switch
  {
    DomainParameterValue.Int64Value  i => new ProtoParameterValue { IntVal    = i.Value },
    DomainParameterValue.DoubleValue d => new ProtoParameterValue { DoubleVal = d.Value },
    DomainParameterValue.BoolValue   b => new ProtoParameterValue { BoolVal   = b.Value },
    DomainParameterValue.StringValue s => new ProtoParameterValue { StringVal = s.Value },
    _                                  => new ProtoParameterValue()
  };

  private static DomainParameterValue ToDomainValue(ProtoParameterValue proto) =>
      proto.ValueCase switch
      {
        ProtoParameterValue.ValueOneofCase.IntVal    => new DomainParameterValue.Int64Value(proto.IntVal),
        ProtoParameterValue.ValueOneofCase.DoubleVal => new DomainParameterValue.DoubleValue(proto.DoubleVal),
        ProtoParameterValue.ValueOneofCase.BoolVal   => new DomainParameterValue.BoolValue(proto.BoolVal),
        ProtoParameterValue.ValueOneofCase.StringVal => new DomainParameterValue.StringValue(proto.StringVal),
        _ => throw new ArgumentException("ParameterValue has no value set")
      };

  private static ProtoCommandResult ToProtoCommandResult(DomainCommandResult result)
  {
    var proto = new ProtoCommandResult { Success = result.Success };
    switch (result.ReturnValue)
    {
      case DomainParameterValue.Int64Value  i: proto.IntVal    = i.Value; break;
      case DomainParameterValue.DoubleValue d: proto.DoubleVal = d.Value; break;
      case DomainParameterValue.BoolValue   b: proto.BoolVal   = b.Value; break;
      case DomainParameterValue.StringValue s: proto.StringVal = s.Value; break;
    }
    return proto;
  }
}
