using System.IO;
using System;
using Core.Logging.Factories;
using Core.Logging.Services;
using InspectionClient.Interfaces;
using InspectionClient.Services;
using InspectionClient.Services.Probes;
using InspectionClient.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using GrpcFrameGrabber = Core.Grpc.FrameGrabber.FrameGrabber;

namespace InspectionClient;

internal static class Startup
{
    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // ── Logging ───────────────────────────────────────────────────────────
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        services.AddSingleton(_ => FileLoggerFactory.Create(logDirectory, fileNamePrefix: "inspection"));
        services.AddSingleton<LogService>();
        services.AddSingleton<IObservableLogService>(sp => sp.GetRequiredService<LogService>());
        services.AddSingleton<MicrosoftLogService>();
        services.AddSingleton<Core.Logging.Interfaces.ILogService>(sp =>
            new CompositeLogService(
                sp.GetRequiredService<LogService>(),
                sp.GetRequiredService<MicrosoftLogService>()));

        // ── gRPC Clients ──────────────────────────────────────────────────────
        var frameGrabberAddress = context.Configuration
            .GetValue<string>("GrpcEndpoints:FileFrameGrabberService")
            ?? "http://localhost:5001";
        var inspectionAddress = context.Configuration
            .GetValue<string>("GrpcEndpoints:InspectionService")
            ?? "http://localhost:5002";

        services.AddGrpcClient<GrpcFrameGrabber.FrameGrabberClient>(o =>
            o.Address = new Uri(frameGrabberAddress));

        // TODO: InspectionService proto가 확정되면 아래 주석을 해제하세요.
        // services.AddGrpcClient<InspectionService.Greeter.GreeterClient>(o =>
        //     o.Address = new Uri(inspectionAddress));

        // ── Equipment Config ──────────────────────────────────────────────────
        services.AddSingleton<EquipmentConfigService>();
        services.AddSingleton<IEquipmentConfigService>(sp => sp.GetRequiredService<EquipmentConfigService>());

        // ── Frame Source ──────────────────────────────────────────────────────
        var useMock = context.Configuration.GetValue<bool>("Features:UseMockFrameSource");
        if (useMock)
        {
          services.AddSingleton<MockFrameSourceService>();
          services.AddSingleton<IFrameSource>(sp => sp.GetRequiredService<MockFrameSourceService>());
          services.AddSingleton<IFrameGrabberController>(sp => sp.GetRequiredService<MockFrameSourceService>());
        }
        else
        {
          services.AddSingleton<FrameSourceService>();
          services.AddSingleton<IFrameSource>(sp => sp.GetRequiredService<FrameSourceService>());
          services.AddSingleton<FrameGrabberControlService>();
          services.AddSingleton<IFrameGrabberController>(sp => sp.GetRequiredService<FrameGrabberControlService>());
        }

        // ── Connection Monitor ────────────────────────────────────────────────
        services.AddSingleton<IConnectionProbe, GrpcFrameGrabberProbe>();
        // InspectionService probe 추가 시: services.AddSingleton<IConnectionProbe, GrpcInspectionProbe>();
        services.AddSingleton<ConnectionMonitor>();
        services.AddSingleton<IServiceConnectionMonitor>(sp => sp.GetRequiredService<ConnectionMonitor>());
        services.AddHostedService(sp => sp.GetRequiredService<ConnectionMonitor>());

        // ── ViewModels ────────────────────────────────────────────────────────
        services.AddTransient<InspectionViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<FrameGrabberControlViewModel>();
        services.AddTransient<OpticSettingViewModel>();
        services.AddTransient<AppSettingViewModel>();
        services.AddTransient<EquipmentSpecViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
