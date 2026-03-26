using Core.Logging.Interfaces;

namespace InspectionClient.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    public HistoryViewModel(ILogService logService) : base(logService) { }
}
