using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using InspectionClient.ViewModels;
using InspectionClient.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InspectionClient;

public partial class App : Application
{
    // Program.Main에서 Host를 Build한 뒤 여기에 주입한다.
    // Avalonia는 XAML 로더에서 기본 생성자로 App을 인스턴스화하므로
    // 생성자 주입 대신 정적 필드를 사용한다.
    internal static IHost Host = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Host.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.Exit += (_, _) =>
            {
                Host.Services.GetRequiredService<ILoggerFactory>().Dispose();
                Host.StopAsync().GetAwaiter().GetResult();
            };

            _ = Host.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
