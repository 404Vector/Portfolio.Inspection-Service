using InspectionClient.Enums;

namespace InspectionClient.Interfaces;

public interface ILogService
{
    void Log(object sender, LogLevel level, string message);

    void Trace(object sender, string message)   => Log(sender, LogLevel.Trace,   message);
    void Debug(object sender, string message)   => Log(sender, LogLevel.Debug,   message);
    void Info(object sender, string message)    => Log(sender, LogLevel.Info,     message);
    void Warning(object sender, string message) => Log(sender, LogLevel.Warning,  message);
    void Error(object sender, string message)   => Log(sender, LogLevel.Error,    message);
}
