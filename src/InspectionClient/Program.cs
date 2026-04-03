using System;
using Avalonia;
using InspectionClient.Infrastructure;
using InspectionClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InspectionClient;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        App.Host = CreateHostBuilder(args).Build();

        // EF Core: DB 및 테이블 생성, 시드 데이터 삽입
        using (var scope = App.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InspectionDbContext>();
            db.Database.EnsureCreated();
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // ConsoleLifetime이 Ctrl+C를 가로채지 않도록 제거한다.
                // 앱 종료는 Avalonia desktop.Exit 핸들러에서 Host.StopAsync()로 처리한다.
                services.AddSingleton<IHostLifetime, NoopHostLifetime>();

                Startup.ConfigureServices(context, services);
            });

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
