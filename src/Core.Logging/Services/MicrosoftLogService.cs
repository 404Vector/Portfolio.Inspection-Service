using Core.Logging.Interfaces;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;
using AppLogLevel = Core.Logging.Enums.LogLevel;

namespace Core.Logging.Services;

public sealed class MicrosoftLogService : ILogService
{
    private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;

    public MicrosoftLogService(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public void Log(object sender, AppLogLevel level, string message)
    {
        var categoryName = ResolveCategoryName(sender);
        var logger = _loggerFactory.CreateLogger(categoryName);
        Microsoft.Extensions.Logging.LoggerExtensions.Log(logger, ToMicrosoftLogLevel(level), "{Message}", message);
    }

    private static string ResolveCategoryName(object sender)
    {
        var name = sender is string s ? s : sender.GetType().Name;
        foreach (var suffix in new[] { "ViewModel", "Service", "View" })
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return name[..^suffix.Length];
        }
        return name;
    }

    private static MsLogLevel ToMicrosoftLogLevel(AppLogLevel level) => level switch
    {
        AppLogLevel.Trace   => MsLogLevel.Trace,
        AppLogLevel.Debug   => MsLogLevel.Debug,
        AppLogLevel.Info    => MsLogLevel.Information,
        AppLogLevel.Warning => MsLogLevel.Warning,
        AppLogLevel.Error   => MsLogLevel.Error,
        _                   => MsLogLevel.None,
    };
}
