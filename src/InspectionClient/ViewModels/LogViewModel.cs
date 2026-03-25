using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using InspectionClient.Enums;
using InspectionClient.Models;
using InspectionClient.Services;

namespace InspectionClient.ViewModels;

public sealed partial class LogViewModel : ViewModelBase, ILogService
{
    private const int MaxEntries = 500;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public void Log(LogLevel level, string message)
    {
        var entry = new LogEntry(System.DateTimeOffset.Now, level, message);

        Dispatcher.UIThread.Post(() =>
        {
            Entries.Add(entry);
            if (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        });
    }

    [RelayCommand]
    private void Clear() => Entries.Clear();
}
