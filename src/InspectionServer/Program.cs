using InspectionServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<WaferInspectionGrpcService>();
app.MapGet("/", () => "InspectionServer (gRPC). Use a gRPC client to interact with this service.");

app.Run();
