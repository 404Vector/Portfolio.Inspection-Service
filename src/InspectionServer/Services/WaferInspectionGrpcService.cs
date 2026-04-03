using Core.Grpc.Inspection;
using Grpc.Core;

namespace InspectionServer.Services;

public sealed class WaferInspectionGrpcService : WaferInspection.WaferInspectionBase
{
  private readonly ILogger<WaferInspectionGrpcService> _logger;

  public WaferInspectionGrpcService(ILogger<WaferInspectionGrpcService> logger)
  {
    _logger = logger;
  }

  // ── 단일 프레임 검사 ──────────────────────────────────────────────────────

  public override Task<InspectFrameResponse> InspectFrame(
      InspectFrameRequest request, ServerCallContext context)
  {
    _logger.LogInformation(
        "InspectFrame called: frameId={FrameId} recipe={RecipeName}",
        request.FrameId, request.Recipe?.RecipeName);

    throw new RpcException(new Status(StatusCode.Unimplemented, "InspectFrame not yet implemented"));
  }

  // ── 잡 라이프사이클 ───────────────────────────────────────────────────────

  public override Task<StartInspectionJobResponse> StartInspectionJob(
      StartInspectionJobRequest request, ServerCallContext context)
  {
    _logger.LogInformation(
        "StartInspectionJob called: jobId={JobId} recipe={RecipeName}",
        request.JobId, request.Recipe?.RecipeName);

    throw new RpcException(new Status(StatusCode.Unimplemented, "StartInspectionJob not yet implemented"));
  }

  public override Task<StopInspectionJobResponse> StopInspectionJob(
      StopInspectionJobRequest request, ServerCallContext context)
  {
    _logger.LogInformation("StopInspectionJob called: jobId={JobId}", request.JobId);

    throw new RpcException(new Status(StatusCode.Unimplemented, "StopInspectionJob not yet implemented"));
  }

  // ── 잡 결과 조회 ──────────────────────────────────────────────────────────

  public override Task<JobSummaryResponse> GetJobSummary(
      GetJobSummaryRequest request, ServerCallContext context)
  {
    _logger.LogInformation("GetJobSummary called: jobId={JobId}", request.JobId);

    throw new RpcException(new Status(StatusCode.Unimplemented, "GetJobSummary not yet implemented"));
  }

  public override Task<JobResultsResponse> GetJobResults(
      GetJobResultsRequest request, ServerCallContext context)
  {
    _logger.LogInformation(
        "GetJobResults called: jobId={JobId} page={Page} pageSize={PageSize}",
        request.JobId, request.Page, request.PageSize);

    throw new RpcException(new Status(StatusCode.Unimplemented, "GetJobResults not yet implemented"));
  }

  // ── 서버 스트리밍 ─────────────────────────────────────────────────────────

  public override async Task SubscribeJobEvents(
      SubscribeJobEventsRequest          request,
      IServerStreamWriter<JobFrameEvent> responseStream,
      ServerCallContext                  context)
  {
    _logger.LogInformation("SubscribeJobEvents called: jobId={JobId}", request.JobId);

    await Task.CompletedTask;

    throw new RpcException(new Status(StatusCode.Unimplemented, "SubscribeJobEvents not yet implemented"));
  }

  // ── 서비스 상태 ───────────────────────────────────────────────────────────

  public override Task<InspectionStatusResponse> GetStatus(
      GetStatusRequest request, ServerCallContext context)
  {
    _logger.LogInformation("GetStatus called");

    throw new RpcException(new Status(StatusCode.Unimplemented, "GetStatus not yet implemented"));
  }
}
