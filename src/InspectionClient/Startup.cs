using System.IO;
using System;
using Core.Logging.Factories;
using Core.Logging.Services;
using InspectionClient.Interfaces;
using InspectionClient.Services;
using InspectionClient.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            .GetValue<string>("GrpcEndpoints:FrameGrabberService")
            ?? "http://localhost:5001";
        var inspectionAddress = context.Configuration
            .GetValue<string>("GrpcEndpoints:InspectionService")
            ?? "http://localhost:5002";

        services.AddGrpcClient<FrameGrabberService.FrameGrabber.FrameGrabberClient>(o =>
            o.Address = new Uri(frameGrabberAddress));

        // TODO: InspectionService proto가 확정되면 아래 주석을 해제하세요.
        // services.AddGrpcClient<InspectionService.Greeter.GreeterClient>(o =>
        //     o.Address = new Uri(inspectionAddress));

        // ── Frame Source ──────────────────────────────────────────────────────
        services.AddSingleton<IFrameSource, MockFrameSourceService>();

        // ── ViewModels ────────────────────────────────────────────────────────
        services.AddTransient<InspectionViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<OpticSettingViewModel>();
        services.AddTransient<AppSettingViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
