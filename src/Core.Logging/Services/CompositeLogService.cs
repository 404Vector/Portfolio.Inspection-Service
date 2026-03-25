using Core.Logging.Enums;
using Core.Logging.Interfaces;

namespace Core.Logging.Services;

public sealed class CompositeLogService : ILogService
{
    private readonly IReadOnlyList<ILogService> _services;

    public CompositeLogService(params ILogService[] services)
    {
        _services = services;
    }

    public void Log(object sender, LogLevel level, string message)
    {
        foreach (var service in _services)
            service.Log(sender, level, message);
    }
}
