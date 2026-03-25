using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var logService  = new LogService();
            var frameSource = new MockFrameSourceService();

            var vm = new MainWindowViewModel(
                new InspectionViewModel(logService),
                new HistoryViewModel(logService),
                new OpticSettingViewModel(logService, frameSource),
                new AppSettingViewModel(logService),
                logService);

            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.Exit += (_, _) => frameSource.Stop();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
