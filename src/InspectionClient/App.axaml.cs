using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Core.Logging.Factories;
using Core.Logging.Services;
using InspectionClient.Services;
using InspectionClient.ViewModels;
using InspectionClient.Views;

namespace InspectionClient;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var logDirectory  = Path.Combine(AppContext.BaseDirectory, "logs");
            var loggerFactory = FileLoggerFactory.Create(logDirectory, fileNamePrefix: "inspection");

            var uiLogService   = new LogService();
            var fileLogService = new MicrosoftLogService(loggerFactory);
            var logService     = new CompositeLogService(uiLogService, fileLogService);

            var frameSource = new MockFrameSourceService();

            var vm = new MainWindowViewModel(
                new InspectionViewModel(logService),
                new HistoryViewModel(logService),
                new OpticSettingViewModel(logService, frameSource),
                new AppSettingViewModel(logService),
                uiLogService);

            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.Exit += (_, _) =>
            {
                frameSource.Stop();
                loggerFactory.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
