using Core.FrameGrabber.Interfaces;
using Core.SharedMemory.Models;
using Core.SharedMemory.Writer;
using FrameGrabberService.Grabbers;
using FrameGrabberService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

// ── Frame Grabber ────────────────────────────────────────────────────────────
// IFrameGrabber 구현체를 교체하는 것으로 실제 카메라로 전환 가능
builder.Services.AddSingleton<IFrameGrabber, MockFrameGrabber>();

// ── Shared Memory Ring Buffer ────────────────────────────────────────────────
var ringBufferOptions = builder.Configuration
    .GetSection("RingBuffer")
    .Get<RingBufferOptions>() ?? new RingBufferOptions();

builder.Services.AddSingleton(ringBufferOptions);
builder.Services.AddSingleton<SharedMemoryRingBuffer>();

// ── Frame Pump (IFrameGrabber → RingBuffer, 단일 프로듀서 보장) ──────────────
// 순수 클래스. lifecycle은 FrameGrabberGrpcService의 StartAcquisition/StopAcquisition이 제어한다.
builder.Services.AddSingleton<FramePump>();

var app = builder.Build();

// ── 앱 종료 시 FramePump 정리 ────────────────────────────────────────────────
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var pump = app.Services.GetRequiredService<FramePump>();
    pump.StopAsync().GetAwaiter().GetResult();
});

app.MapGrpcService<FrameGrabberGrpcService>();
app.MapGet("/", () => "FrameGrabberService (gRPC). Use a gRPC client to interact with this service.");

app.Run();
