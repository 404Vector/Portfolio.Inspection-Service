using System.Collections.ObjectModel;
using InspectionClient.Models;

namespace InspectionClient.Interfaces;

public interface IObservableLogService : ILogService
{
    ObservableCollection<LogEntry> Entries { get; }
}
