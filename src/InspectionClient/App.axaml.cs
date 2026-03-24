using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var vm = new MainWindowViewModel(
                new InspectionViewModel(),
                new HistoryViewModel(),
                new OpticSettingViewModel(),
                new AppSettingViewModel());

            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}