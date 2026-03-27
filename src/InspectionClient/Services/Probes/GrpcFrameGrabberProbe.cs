using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Grpc.FrameGrabber;
using Core.Logging.Interfaces;
using Grpc.Core;
using InspectionClient.Interfaces;

using GrpcFrameGrabber = Core.Grpc.FrameGrabber.FrameGrabber;

namespace InspectionClient.Services.Probes;

/// <summary>
/// FrameGrabber gRPC 서비스의 연결 가능 여부를 확인하는 probe.
/// GetCapabilities RPC를 호출해 서비스가 응답하는지 확인한다.
/// </summary>
public sealed class GrpcFrameGrabberProbe : IConnectionProbe
{
  public const string Key = "FrameGrabber";
  public string ServiceKey => Key;

  private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

  private readonly GrpcFrameGrabber.FrameGrabberClient _client;
  private readonly ILogService _log;

  public GrpcFrameGrabberProbe(
      GrpcFrameGrabber.FrameGrabberClient client,
      ILogService logService)
  {
    _client = client;
    _log    = logService;
  }

  public async Task<bool> CheckAsync(CancellationToken ct)
  {
    try
    {
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      cts.CancelAfter(Timeout);

      await _client.GetCapabilitiesAsync(
          new GetCapabilitiesRequest(),
          cancellationToken: cts.Token);

      return true;
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
      // probe timeout
      return false;
    }
    catch (OperationCanceledException)
    {
      // 전체 루프 취소 — 상태 변경 없이 종료
      return false;
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
    {
      return false;
    }
    catch (Exception ex)
    {
      _log.Warning(this, $"FrameGrabber probe error: {ex.Message}");
      return false;
    }
  }
}
