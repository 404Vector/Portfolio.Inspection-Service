using System.Collections.ObjectModel;
using Core.Logging.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Interfaces;

public interface IObservableLogService : ILogService
{
    ObservableCollection<LogEntry> Entries { get; }
}
