using InspectionClient.Enums;

namespace InspectionClient.Services;

public interface ILogService
{
    void Log(LogLevel level, string message);

    void Trace(string message)   => Log(LogLevel.Trace,   message);
    void Debug(string message)   => Log(LogLevel.Debug,   message);
    void Info(string message)    => Log(LogLevel.Info,     message);
    void Warning(string message) => Log(LogLevel.Warning,  message);
    void Error(string message)   => Log(LogLevel.Error,    message);
}
