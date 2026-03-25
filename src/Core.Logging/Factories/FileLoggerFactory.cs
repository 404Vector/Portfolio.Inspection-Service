using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Core.Logging.Factories;

public static class FileLoggerFactory
{
    /// <summary>
    /// 지정된 경로에 일별 롤링 파일로 기록하는 ILoggerFactory를 생성합니다.
    /// </summary>
    /// <param name="logDirectory">로그 파일을 저장할 디렉터리 경로</param>
    /// <param name="fileNamePrefix">로그 파일 이름 접두사 (기본값: "app")</param>
    public static ILoggerFactory Create(string logDirectory, string fileNamePrefix = "app")
    {
        Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(logDirectory, $"{fileNamePrefix}-.log");

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSerilog(serilogLogger, dispose: true);
        });
    }
}
