using FrameGrabberService.Grabbers;
using FrameGrabberService.Services;
using FrameGrabberService.SharedMemory;

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

// ── Background Pump (IFrameGrabber → RingBuffer, 단일 프로듀서 보장) ─────────
builder.Services.AddHostedService<FramePumpService>();

var app = builder.Build();

app.MapGrpcService<FrameGrabberGrpcService>();
app.MapGet("/", () => "FrameGrabberService (gRPC). Use a gRPC client to interact with this service.");

app.Run();
