using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using InspectionClient.Enums;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Services;

public sealed class LogService : IObservableLogService
{
    private const int MaxEntries = 500;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public void Log(object sender, LogLevel level, string message)
    {
        var senderName = ResolveSenderName(sender);
        var entry = new LogEntry(DateTimeOffset.Now, level, senderName, message);

        Dispatcher.UIThread.Post(() =>
        {
            Entries.Add(entry);
            if (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        });
    }

    public void Clear() => Entries.Clear();

    private static string ResolveSenderName(object sender)
    {
        var name = sender is string s ? s : sender.GetType().Name;
        foreach (var suffix in new[] { "ViewModel", "Service", "View" })
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return name[..^suffix.Length];
        }
        return name;
    }
}
