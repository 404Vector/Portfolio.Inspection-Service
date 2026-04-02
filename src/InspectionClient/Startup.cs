using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Core.Logging.Factories;
using Core.Logging.Services;
using InspectionClient.Interfaces;
using InspectionClient.Repositories;
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

        services.AddGrpcClient<GrpcFrameGrabber.FrameGrabberClient>(o =>
            o.Address = new Uri(frameGrabberAddress));

        // TODO: InspectionService proto가 확정되면 아래 주석을 해제하세요.
        // services.AddGrpcClient<InspectionService.Greeter.GreeterClient>(o =>
        //     o.Address = new Uri(inspectionAddress));

        // ── Equipment Config ──────────────────────────────────────────────────
        services.AddSingleton<EquipmentConfigService>();
        services.AddSingleton<IEquipmentConfigService>(sp => sp.GetRequiredService<EquipmentConfigService>());

        // ── Frame Source ──────────────────────────────────────────────────────
        var useMockFrame = context.Configuration.GetValue<bool>("Features:UseMockFrameSource");
        if (useMockFrame)
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

        // ── Inspection Service ────────────────────────────────────────────────
        services.AddSingleton<MockInspectionService>();
        services.AddSingleton<IInspectionService>(sp => sp.GetRequiredService<MockInspectionService>());

        // ── Connection Monitor ────────────────────────────────────────────────
        services.AddSingleton<IConnectionProbe, GrpcFrameGrabberProbe>();
        services.AddSingleton<ConnectionMonitor>();
        services.AddSingleton<IServiceConnectionMonitor>(sp => sp.GetRequiredService<ConnectionMonitor>());
        services.AddHostedService(sp => sp.GetRequiredService<ConnectionMonitor>());

        // ── Database ──────────────────────────────────────────────────────────
        services.AddSingleton<InspectionDatabase>();

        // ── Die Rendering ─────────────────────────────────────────────────────
        services.AddSingleton<IDieImageRenderer, DieImageRenderer>();

        // ── Repositories ──────────────────────────────────────────────────────
        services.AddSingleton<IDieRenderingParametersRepository, SqliteDieRenderingParametersRepository>();
        services.AddSingleton<IWaferInfoRepository, SqliteWaferInfoRepository>();
        services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();
        services.AddSingleton<IDieSpotRecipeRepository, SqliteDieSpotRecipeRepository>();
        services.AddSingleton<IInspectionResultRepository, SqliteInspectionResultRepository>();
        services.AddSingleton<IUserAnnotationRepository, SqliteUserAnnotationRepository>();

        // ── ViewModels ────────────────────────────────────────────────────────
        services.AddSingleton<DieSetupWorkflowViewModel>();
        services.AddSingleton<WaferSetupWorkflowViewModel>();
        services.AddSingleton<WaferSurfaceRecipeWorkflowViewModel>();
        services.AddSingleton<DieSpotRecipeWorkflowViewModel>();
        services.AddSingleton<RecipeSetupWorkflowViewModel>();
        services.AddSingleton<SetupWorkflowViewModel>();
        services.AddSingleton<InspectionWorkflowViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<FileFrameGrabberViewModel>();
        services.AddTransient<FrameGrabberViewModelBase>(sp => sp.GetRequiredService<FileFrameGrabberViewModel>());
        services.AddTransient<AppSettingViewModel>();
        services.AddTransient<EquipmentSpecViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
