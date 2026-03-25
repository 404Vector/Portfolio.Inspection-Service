using System;
using Core.Logging.Enums;

namespace InspectionClient.Models;

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel       Level,
    string         Sender,
    string         Message)
{
    public string Display =>
        $"[{Timestamp:HH:mm:ss}] [{Level,-7}] [{Sender}] {Message}";
}
