using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Grpc.FrameGrabber;
using Core.Logging.Interfaces;
using Grpc.Core;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using InspectionClient.Utils;

using GrpcFrameGrabber = Core.Grpc.FrameGrabber.FrameGrabber;

namespace InspectionClient.Services;

public sealed class FrameGrabberControlService : IFrameGrabberController
{
  private readonly GrpcFrameGrabber.FrameGrabberClient _grpcClient;
  private readonly ILogService _log;

  public FrameGrabberControlService(
      GrpcFrameGrabber.FrameGrabberClient grpcClient,
      ILogService logService)
  {
    _grpcClient = grpcClient;
    _log        = logService;
  }

  public async Task<StatusResponse> GetStatusAsync(CancellationToken ct = default)
  {
    return await _grpcClient.GetStatusAsync(new GetStatusRequest(), cancellationToken: ct);
  }

  public void SetProperty(string key, object? value)
  {
    var proto = GrabberProtoMapper.ToParameterValue(value);

    _ = Task.Run(async () =>
    {
      try
      {
        var response = await _grpcClient.SetParameterAsync(
            new SetParameterRequest { Key = key, Value = proto },
            cancellationToken: CancellationToken.None);

        if (!response.Success)
          _log.Warning(this, $"SetParameter '{key}' failed: {response.Message}");
      }
      catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
      catch (Exception ex)
      {
        _log.Warning(this, $"SetParameter '{key}' error: {ex.Message}");
      }
    });
  }

  public async Task<(bool Success, string Message)> SetParameterAsync(
      string key, object? value, CancellationToken ct = default)
  {
    try
    {
      var proto    = GrabberProtoMapper.ToParameterValue(value);
      var response = await _grpcClient.SetParameterAsync(
          new SetParameterRequest { Key = key, Value = proto },
          cancellationToken: ct);
      return (response.Success, response.Message);
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
    {
      return (false, "Cancelled");
    }
    catch (Exception ex)
    {
      return (false, ex.Message);
    }
  }

  public async Task StartAcquisitionAsync(CancellationToken ct = default)
  {
    try
    {
      var response = await _grpcClient.StartAcquisitionAsync(
          new StartAcquisitionRequest(), cancellationToken: ct);

      if (!response.Success)
        _log.Warning(this, $"StartAcquisition failed: {response.Message}");
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
    catch (Exception ex)
    {
      _log.Warning(this, $"StartAcquisition error: {ex.Message}");
    }
  }

  public async Task StopAcquisitionAsync(CancellationToken ct = default)
  {
    try
    {
      var response = await _grpcClient.StopAcquisitionAsync(
          new StopAcquisitionRequest(), cancellationToken: ct);

      if (!response.Success)
        _log.Warning(this, $"StopAcquisition failed: {response.Message}");
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
    catch (Exception ex)
    {
      _log.Warning(this, $"StopAcquisition error: {ex.Message}");
    }
  }

  public async Task TriggerFrameAsync(CancellationToken ct = default)
  {
    try
    {
      await _grpcClient.TriggerFrameAsync(
          new TriggerFrameRequest(), cancellationToken: ct);
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
    catch (Exception ex)
    {
      _log.Warning(this, $"TriggerFrame error: {ex.Message}");
    }
  }

  public async Task<object?> GetParameterAsync(string key, CancellationToken ct = default)
  {
    try
    {
      var response = await _grpcClient.GetParameterAsync(
          new GetParameterRequest { Key = key },
          cancellationToken: ct);

      return response.Value is { ValueCase: not ParameterValue.ValueOneofCase.None }
          ? GrabberProtoMapper.ToObject(response.Value)
          : null;
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
    {
      return null;
    }
    catch (Exception ex)
    {
      _log.Warning(this, $"GetParameter '{key}' error: {ex.Message}");
      return null;
    }
  }

  public async Task<GrabberCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
  {
    try
    {
      var response = await _grpcClient.GetCapabilitiesAsync(
          new GetCapabilitiesRequest(), cancellationToken: ct);

      var parameters = response.Parameters
          .Select(p =>
          {
            var item = new GrabberParameterItem
            {
              Key          = p.Key,
              DisplayName  = p.DisplayName,
              ValueType    = p.ValueType,
              MinValue     = p.MinValue     is { ValueCase: not ParameterValue.ValueOneofCase.None } ? GrabberProtoMapper.ToObject(p.MinValue)     : null,
              MaxValue     = p.MaxValue     is { ValueCase: not ParameterValue.ValueOneofCase.None } ? GrabberProtoMapper.ToObject(p.MaxValue)     : null,
              CurrentValue = p.DefaultValue is { ValueCase: not ParameterValue.ValueOneofCase.None } ? GrabberProtoMapper.ToObject(p.DefaultValue) : null,
            };
            item.OriginalValue = item.CurrentValue;
            return item;
          })
          .ToList();

      var commands = response.Commands
          .Select(c => new GrabberCommandItem(c.Key, c.DisplayName, c.Description))
          .ToList();

      return new GrabberCapabilities(parameters, commands);
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
    {
      _log.Warning(this, $"GetCapabilities unavailable: {ex.Status.Detail}");
      return GrabberCapabilities.Empty;
    }
    catch (Exception ex)
    {
      _log.Warning(this, $"GetCapabilities error: {ex.Message}");
      return GrabberCapabilities.Empty;
    }
  }

  public async Task ExecuteCommandAsync(string commandKey, CancellationToken ct = default)
  {
    try
    {
      var result = await _grpcClient.ExecuteCommandAsync(
          new ExecuteCommandRequest { Command = commandKey },
          cancellationToken: ct);

      if (!result.Success)
        _log.Warning(this, $"ExecuteCommand '{commandKey}' returned failure");
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
    catch (Exception ex)
    {
      _log.Warning(this, $"ExecuteCommand '{commandKey}' error: {ex.Message}");
    }
  }
}
