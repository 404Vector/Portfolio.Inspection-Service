using Core.FrameGrabber.Interfaces;
using Core.SharedMemory.Models;
using Core.SharedMemory.Writer;
using FileFrameGrabberService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

// ── Frame Grabber ────────────────────────────────────────────────────────────
// IFrameGrabber 구현체를 교체하는 것으로 실제 카메라로 전환 가능
builder.Services.AddSingleton<GradientFrameSynthesizerService>();
builder.Services.AddSingleton<FileImageFrameSynthesizerService>();
builder.Services.AddSingleton<GrabberParameterStoreService>();
builder.Services.AddSingleton<IFrameGrabber, FileFrameGrabberService.Services.FileFrameGrabberService>();

// ── Shared Memory Ring Buffer ────────────────────────────────────────────────
var ringBufferOptions = builder.Configuration
    .GetSection("RingBuffer")
    .Get<RingBufferOptions>() ?? new RingBufferOptions();

builder.Services.AddSingleton(ringBufferOptions);
builder.Services.AddSingleton<SharedMemoryRingBuffer>();

// ── Frame Pump (IFrameGrabber → RingBuffer, 단일 프로듀서 보장) ──────────────
// 획득 시작/정지는 RPC가 제어. 앱 종료 시 FramePumpHostedService.StopAsync가 정리.
builder.Services.AddSingleton<FramePumpHostedService>();
builder.Services.AddHostedService(p => p.GetRequiredService<FramePumpHostedService>());

var app = builder.Build();

app.MapGrpcService<FrameGrabberGrpcService>();
app.MapGet("/", () => "FileFrameGrabberService (gRPC). Use a gRPC client to interact with this service.");

app.Run();
